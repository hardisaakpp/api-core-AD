using System.DirectoryServices.Protocols;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SCNexus.Core.API.Data;
using SCNexus.Core.API.Models;

namespace SCNexus.Core.API.Services
{
    public partial class AdLdapSyncService
    {
        private const string AttrSamAccountName = "sAMAccountName";
        private const string AttrMail = "mail";
        private const string AttrDisplayName = "displayName";
        private const string AttrUserAccountControl = "userAccountControl";

        private readonly IConfiguration _config;
        private readonly ILogger<AdLdapSyncService> _logger;

        public AdLdapSyncService(IConfiguration config, ILogger<AdLdapSyncService> logger)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Full synchronisation of Active Directory users into the local Identity store.
        /// Paged search (AD refuses more than 1000 objects per request without paging, which is
        /// exactly why the previous version failed with "The size limit was exceeded"), person
        /// objects only (computers are also objectClass=user), and a summary instead of exceptions
        /// so the nightly job and the manual trigger can report what happened.
        /// </summary>
        public async Task<LdapSyncResult> SyncUsersAsync(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            AppDbContext db,
            string trigger = "manual",
            CancellationToken cancellationToken = default)
        {
            var startedAt = DateTimeOffset.UtcNow;
            var settings = ReadSettings();
            var ctx = new SyncContext { UserManager = userManager, RoleManager = roleManager, DefaultRole = settings.DefaultRole };
            var counters = ctx.Counters;

            LdapSyncResult Finish(bool success, string message) =>
                new(success, message, counters.Processed, counters.Created, counters.Updated, counters.Deactivated,
                    counters.Skipped, counters.Errors, startedAt, DateTimeOffset.UtcNow, trigger);

            if (!settings.IsComplete)
            {
                _logger.LogWarning("LDAP config incomplete (Host/BindDn/BindPassword required)");
                return Finish(false, "LDAP no está configurado (Host/BindDn/BindPassword).");
            }

            var timeout = TimeSpan.FromMinutes(2);
            var (connection, connectionError) = OpenServiceConnection(settings, timeout, "sync users");
            if (connection is null)
            {
                return Finish(false, connectionError ?? "No se pudo conectar a Active Directory.");
            }

            using var conn = connection;
            try
            {
                await SyncAllPagesAsync(conn, settings, ReadPageSize(), timeout, ctx, cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(ex, "LDAP sync cancelled after {Pages} page(s), {Processed} entries", ctx.Pages, counters.Processed);
                return Finish(false, "Sincronización cancelada.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LDAP search failed after {Pages} page(s), {Processed} entries", ctx.Pages, counters.Processed);
                return Finish(false, $"Error consultando Active Directory tras {counters.Processed} usuarios: {ex.Message}");
            }

            if (ReadDeactivateMissing() && ctx.SeenExternalIds.Count > 0)
            {
                counters.Deactivated = await DeactivateMissingAsync(userManager, ctx.SeenExternalIds);
            }

            var summary = $"AD sync ({trigger}): {counters.Processed} procesados, {counters.Created} creados, {counters.Updated} actualizados, " +
                          $"{counters.Deactivated} desactivados, {counters.Skipped} omitidos, {counters.Errors} errores, {ctx.Pages} página(s).";
            _logger.LogInformation("{Summary}", summary);
            return Finish(counters.Errors == 0, summary);
        }

        private sealed record LdapSettings(
            string? Host,
            int Port,
            string? BindDn,
            string? BindPassword,
            string SearchBase,
            string UserFilter,
            string DefaultRole,
            bool UseLdaps)
        {
            public bool IsComplete =>
                !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(BindDn) && !string.IsNullOrWhiteSpace(BindPassword);
        }

        /// <summary>Everything the synchroniser needs while walking the directory, plus the running counters.</summary>
        private sealed class SyncContext
        {
            public required UserManager<AppUser> UserManager { get; init; }
            public required RoleManager<IdentityRole> RoleManager { get; init; }
            public required string DefaultRole { get; init; }
            public HashSet<string> SeenExternalIds { get; } = new(StringComparer.OrdinalIgnoreCase);
            public SyncCounters Counters { get; } = new();
            /// <summary>Pages fetched so far; kept here so a failure can report how far it got.</summary>
            public int Pages { get; set; }
        }

        private LdapSettings ReadSettings()
        {
            var portValue = _config["Ldap:Port"];
            if (!int.TryParse(portValue, out var port))
            {
                _logger.LogWarning("Invalid LDAP port value '{PortValue}'. Falling back to 389.", portValue);
                port = 389;
            }

            return new LdapSettings(
                _config["Ldap:Host"],
                port,
                _config["Ldap:BindDn"],
                _config["Ldap:BindPassword"],
                _config["Ldap:SearchBase"] ?? string.Empty,
                _config["Ldap:UserFilter"] ?? "(objectClass=user)",
                _config["Ldap:DefaultRole"] ?? "User",
                bool.TryParse(_config["Ldap:UseLdaps"], out var ssl) && ssl);
        }

        private int ReadPageSize() =>
            int.TryParse(_config["Ldap:PageSize"], out var ps) && ps is > 0 and <= 1000 ? ps : 500;

        private bool ReadDeactivateMissing() =>
            bool.TryParse(_config["Ldap:NightlySync:DeactivateMissing"], out var dm) && dm;

        /// <summary>Connection bound with the service account, or null plus the user-facing reason.</summary>
        private (LdapConnection? Connection, string? Error) OpenServiceConnection(LdapSettings settings, TimeSpan timeout, string purpose)
        {
            LdapDirectoryIdentifier ldapIdentifier;
            try
            {
                ldapIdentifier = new LdapDirectoryIdentifier(settings.Host, settings.Port);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is TypeInitializationException)
            {
                _logger.LogError(ex, "LDAP native library not available (libldap). Cannot {Purpose}.", purpose);
                return (null, "Librería LDAP nativa no disponible en el servidor (libldap).");
            }

            var conn = new LdapConnection(ldapIdentifier) { AuthType = AuthType.Basic };
            try
            {
                conn.SessionOptions.ProtocolVersion = 3;
                conn.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
                conn.SessionOptions.SecureSocketLayer = settings.UseLdaps;
                conn.Timeout = timeout;
                conn.Bind(new NetworkCredential(settings.BindDn, settings.BindPassword));
                return (conn, null);
            }
            catch (Exception ex)
            {
                conn.Dispose();
                _logger.LogError(ex, "Failed to bind to LDAP");
                return (null, $"No se pudo conectar a Active Directory ({settings.Host}:{settings.Port}): {ex.Message}");
            }
        }

        /// <summary>Walks the paged search and upserts every entry. Throws on cancellation or directory errors.</summary>
        private async Task SyncAllPagesAsync(
            LdapConnection conn,
            LdapSettings settings,
            int pageSize,
            TimeSpan timeout,
            SyncContext ctx,
            CancellationToken cancellationToken)
        {
            // Person accounts only: computers are objectClass=user too, and service/machine accounts
            // (sAMAccountName ending in $) must never become login users.
            var filter = $"(&{settings.UserFilter}(objectCategory=person)(!({AttrSamAccountName}=*$)))";
            var attributes = new[] { AttrSamAccountName, AttrMail, AttrDisplayName, AttrUserAccountControl, "objectGUID" };
            var pageControl = new PageResultRequestControl(pageSize);
            var request = new SearchRequest(settings.SearchBase, filter, SearchScope.Subtree, attributes) { TimeLimit = timeout };
            request.Controls.Add(pageControl);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var response = (SearchResponse)conn.SendRequest(request, timeout);
                ctx.Pages++;

                foreach (SearchResultEntry entry in response.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ctx.Counters.Add(await UpsertFromEntryAsync(entry, ctx));
                }

                var pageResponse = response.Controls.OfType<PageResultResponseControl>().FirstOrDefault();
                if (pageResponse is null || pageResponse.Cookie.Length == 0)
                {
                    return;
                }

                pageControl.Cookie = pageResponse.Cookie;
            }
        }

        private enum UpsertOutcome { Created, Updated, Unchanged, Skipped, Error }

        private sealed class SyncCounters
        {
            public int Processed;
            public int Created;
            public int Updated;
            public int Deactivated;
            public int Skipped;
            public int Errors;

            public void Add(UpsertOutcome outcome)
            {
                switch (outcome)
                {
                    case UpsertOutcome.Created: Processed++; Created++; break;
                    case UpsertOutcome.Updated: Processed++; Updated++; break;
                    case UpsertOutcome.Unchanged: Processed++; break;
                    case UpsertOutcome.Skipped: Skipped++; break;
                    case UpsertOutcome.Error: Errors++; break;
                }
            }
        }

        // Identity's default UserOptions.AllowedUserNameCharacters.
        [GeneratedRegex("^[A-Za-z0-9\\-._@+]+$")]
        private static partial Regex ValidUserName();

        // AD attributes sometimes carry trailing spaces (typed by hand in the AD console); trimmed,
        // otherwise Identity rejects the user name and the account is skipped.
        private static string? Attr(SearchResultEntry entry, string name)
        {
            var value = entry.Attributes[name]?.GetValues(typeof(string)).FirstOrDefault()?.ToString()?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>What an AD entry contributes to the local account. ExternalId is sAMAccountName, or mail when missing.</summary>
        private sealed record AdEntry(string? Sam, string? Mail, string? Display, bool Enabled, string ExternalId);

        /// <summary>Null when the entry has no usable identifier (neither sAMAccountName nor mail).</summary>
        private static AdEntry? ReadEntry(SearchResultEntry entry)
        {
            var sam = Attr(entry, AttrSamAccountName);
            var mail = Attr(entry, AttrMail);
            var uacStr = Attr(entry, AttrUserAccountControl);
            var enabled = !(int.TryParse(uacStr, out var uac) && (uac & 2) != 0); // UAC & 2 == ACCOUNTDISABLE

            var externalId = sam ?? mail;
            return string.IsNullOrWhiteSpace(externalId)
                ? null
                : new AdEntry(sam, mail, Attr(entry, AttrDisplayName), enabled, externalId);
        }

        private async Task<UpsertOutcome> UpsertFromEntryAsync(SearchResultEntry entry, SyncContext ctx)
        {
            try
            {
                var ad = ReadEntry(entry);
                if (ad is null)
                {
                    return UpsertOutcome.Skipped;
                }

                ctx.SeenExternalIds.Add(ad.ExternalId);

                var local = await FindLocalUserAsync(ctx.UserManager, ad);
                return local is null
                    ? await CreateFromEntryAsync(entry.DistinguishedName, ad, ctx)
                    : await UpdateFromEntryAsync(entry.DistinguishedName, ad, local, ctx.UserManager);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LDAP entry {Dn}", entry.DistinguishedName);
                return UpsertOutcome.Error;
            }
        }

        private static async Task<AppUser?> FindLocalUserAsync(UserManager<AppUser> userManager, AdEntry ad)
        {
            var externalId = ad.ExternalId;
            var local = await userManager.Users.FirstOrDefaultAsync(u => u.ExternalProvider == "LDAP" && u.ExternalId == externalId);
            if (local == null && !string.IsNullOrWhiteSpace(ad.Mail))
            {
                local = await userManager.FindByEmailAsync(ad.Mail);
            }
            if (local == null && !string.IsNullOrWhiteSpace(ad.Sam))
            {
                // Users created by the login flow or by hand with the AD login as user name.
                local = await userManager.FindByNameAsync(ad.Sam);
            }
            return local;
        }

        private async Task<UpsertOutcome> CreateFromEntryAsync(string dn, AdEntry ad, SyncContext ctx)
        {
            var userName = !string.IsNullOrWhiteSpace(ad.Mail) ? ad.Mail : ad.ExternalId;
            if (!ValidUserName().IsMatch(userName))
            {
                _logger.LogWarning("LDAP entry {Dn} skipped: user name '{UserName}' has characters Identity does not allow", dn, userName);
                return UpsertOutcome.Skipped;
            }

            var newUser = new AppUser
            {
                UserName = userName,
                Email = ad.Mail,
                FullName = ad.Display ?? string.Empty,
                ExternalProvider = "LDAP",
                ExternalId = ad.ExternalId,
                IsActive = ad.Enabled,
                CreatedAt = DateTime.UtcNow
            };

            var createRes = await ctx.UserManager.CreateAsync(newUser);
            if (!createRes.Succeeded)
            {
                _logger.LogWarning("LDAP entry {Dn} could not be created as '{UserName}': {Errors}", dn, userName,
                    string.Join("; ", createRes.Errors.Select(e => e.Description)));
                return UpsertOutcome.Error;
            }

            if (!await ctx.RoleManager.RoleExistsAsync(ctx.DefaultRole)) await ctx.RoleManager.CreateAsync(new IdentityRole(ctx.DefaultRole));
            await ctx.UserManager.AddToRoleAsync(newUser, ctx.DefaultRole);
            return UpsertOutcome.Created;
        }

        /// <summary>Copies mail, display name, active flag and LDAP provenance onto the local user; true when anything changed.</summary>
        private static bool ApplyAdChanges(AppUser local, AdEntry ad)
        {
            var changed = false;
            if (!string.IsNullOrWhiteSpace(ad.Mail) && local.Email != ad.Mail) { local.Email = ad.Mail; local.UserName = ad.Mail; changed = true; }
            if (!string.IsNullOrWhiteSpace(ad.Display) && local.FullName != ad.Display) { local.FullName = ad.Display; changed = true; }
            if (local.IsActive != ad.Enabled) { local.IsActive = ad.Enabled; changed = true; }
            if (string.IsNullOrWhiteSpace(local.ExternalProvider)) { local.ExternalProvider = "LDAP"; changed = true; }
            if (string.IsNullOrWhiteSpace(local.ExternalId)) { local.ExternalId = ad.ExternalId; changed = true; }
            return changed;
        }

        private async Task<UpsertOutcome> UpdateFromEntryAsync(string dn, AdEntry ad, AppUser local, UserManager<AppUser> userManager)
        {
            if (!ApplyAdChanges(local, ad))
            {
                return UpsertOutcome.Unchanged;
            }

            var updateRes = await userManager.UpdateAsync(local);
            if (!updateRes.Succeeded)
            {
                _logger.LogWarning("LDAP entry {Dn} could not update local user {UserId}: {Errors}", dn, local.Id,
                    string.Join("; ", updateRes.Errors.Select(e => e.Description)));
                return UpsertOutcome.Error;
            }

            return UpsertOutcome.Updated;
        }

        /// <summary>Optional (Ldap:NightlySync:DeactivateMissing): LDAP users no longer returned by AD lose IsActive. Never deletes.</summary>
        private async Task<int> DeactivateMissingAsync(UserManager<AppUser> userManager, HashSet<string> seenExternalIds)
        {
            var candidates = await userManager.Users
                .Where(u => u.ExternalProvider == "LDAP" && u.IsActive && u.ExternalId != null)
                .ToListAsync();

            var deactivated = 0;
            foreach (var user in candidates.Where(u => !seenExternalIds.Contains(u.ExternalId!)))
            {
                user.IsActive = false;
                var res = await userManager.UpdateAsync(user);
                if (res.Succeeded)
                {
                    deactivated++;
                    _logger.LogInformation("LDAP user {UserName} deactivated: no longer present in Active Directory", user.UserName);
                }
            }

            return deactivated;
        }

        public async Task<AppUser?> AuthenticateLdapUserAsync(string userName, string password, UserManager<AppUser> userManager)
        {
            var host = _config["Ldap:Host"];
            var portValue = _config["Ldap:Port"];
            if (!int.TryParse(portValue, out var port))
            {
                _logger.LogWarning("Invalid LDAP port value '{PortValue}'. Falling back to 389.", portValue);
                port = 389;
            }
            var bindDn = _config["Ldap:BindDn"];
            var bindPassword = _config["Ldap:BindPassword"];
            var searchBase = _config["Ldap:SearchBase"] ?? string.Empty;
            var userFilter = _config["Ldap:UserFilter"] ?? "(objectClass=user)";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(bindDn) || string.IsNullOrWhiteSpace(bindPassword))
            {
                _logger.LogWarning("LDAP config incomplete (Host/BindDn/BindPassword required)");
                return null;
            }

            var useLdaps = bool.TryParse(_config["Ldap:UseLdaps"], out var ssl) && ssl;
            LdapDirectoryIdentifier ldapIdentifier;
            try
            {
                ldapIdentifier = new LdapDirectoryIdentifier(host, port);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is TypeInitializationException)
            {
                _logger.LogError(ex, "LDAP native library not available (libldap). Cannot authenticate via Active Directory.");
                return null;
            }

            var ldapTimeout = TimeSpan.FromSeconds(30);
            using var conn = new LdapConnection(ldapIdentifier) { AuthType = AuthType.Basic };
            try
            {
                conn.SessionOptions.ProtocolVersion = 3;
                conn.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
                conn.SessionOptions.SecureSocketLayer = useLdaps;
                conn.Timeout = ldapTimeout;
                // Primero hacer bind con el usuario de servicio para buscar al usuario
                var serviceCred = new NetworkCredential(bindDn, bindPassword);
                conn.Bind(serviceCred);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bind to LDAP with service account");
                return null;
            }

            // Buscar el usuario en LDAP
            var searchFilter = $"(&{userFilter}(|(sAMAccountName={userName})(mail={userName})(userPrincipalName={userName})))";
            var searchRequest = new SearchRequest(searchBase, searchFilter, SearchScope.Subtree, new[] { AttrSamAccountName, AttrMail, AttrDisplayName, "userPrincipalName", "distinguishedName", AttrUserAccountControl });
            searchRequest.TimeLimit = ldapTimeout;

            SearchResponse? searchResponse = null;
            try
            {
                searchResponse = (SearchResponse)conn.SendRequest(searchRequest, ldapTimeout);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to search for user {UserName} in LDAP", userName);
                return null;
            }

            if (searchResponse.Entries.Count == 0)
            {
                _logger.LogWarning("User {UserName} not found in LDAP", userName);
                return null;
            }

            var entry = searchResponse.Entries[0];
            var userDn = entry.DistinguishedName;
            var sam = entry.Attributes[AttrSamAccountName]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var mail = entry.Attributes[AttrMail]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var display = entry.Attributes[AttrDisplayName]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var upn = entry.Attributes["userPrincipalName"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var uacStr = entry.Attributes[AttrUserAccountControl]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            
            bool enabled = true;
            if (int.TryParse(uacStr, out var uac)) enabled = (uac & 2) == 0; // UAC & 2 == ACCOUNTDISABLE
            
            if (!enabled)
            {
                _logger.LogWarning("User {UserName} account is disabled in AD", userName);
                return null;
            }

            // Intentar autenticar con las credenciales del usuario
            using var authConn = new LdapConnection(ldapIdentifier)
            {
                AuthType = AuthType.Basic
            };
            try
            {
                authConn.SessionOptions.ProtocolVersion = 3;
                authConn.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
                authConn.SessionOptions.SecureSocketLayer = useLdaps;
                authConn.Timeout = TimeSpan.FromSeconds(30);
                // Intentar bind con el DN del usuario y su contraseña
                var userCred = new NetworkCredential(userDn, password);
                authConn.Bind(userCred);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to authenticate user {UserName} with provided password", userName);
                return null;
            }

            // Si llegamos aquí, la autenticación fue exitosa
            // Buscar o crear el usuario local
            var externalId = sam ?? mail ?? upn;
            if (string.IsNullOrWhiteSpace(externalId))
            {
                _logger.LogWarning("Could not determine external ID for user {UserName}", userName);
                return null;
            }

            var localUser = await userManager.Users.FirstOrDefaultAsync(u => u.ExternalProvider == "LDAP" && u.ExternalId == externalId);
            if (localUser == null && !string.IsNullOrWhiteSpace(mail))
            {
                localUser = await userManager.FindByEmailAsync(mail);
            }

            if (localUser == null)
            {
                // Crear el usuario local si no existe
                localUser = new AppUser
                {
                    UserName = !string.IsNullOrWhiteSpace(mail) ? mail : externalId,
                    Email = mail,
                    FullName = display ?? string.Empty,
                    ExternalProvider = "LDAP",
                    ExternalId = externalId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(localUser);
                if (!createResult.Succeeded)
                {
                    _logger.LogError("Failed to create local user for {UserName}: {Errors}", userName, string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    return null;
                }
            }
            else
            {
                // Actualizar información si es necesario
                var changed = false;
                if (!string.IsNullOrWhiteSpace(mail) && localUser.Email != mail) { localUser.Email = mail; changed = true; }
                if (!string.IsNullOrWhiteSpace(display) && localUser.FullName != display) { localUser.FullName = display; changed = true; }
                if (localUser.IsActive != enabled) { localUser.IsActive = enabled; changed = true; }
                if (string.IsNullOrWhiteSpace(localUser.ExternalProvider)) { localUser.ExternalProvider = "LDAP"; changed = true; }
                if (string.IsNullOrWhiteSpace(localUser.ExternalId)) { localUser.ExternalId = externalId; changed = true; }
                if (changed) await userManager.UpdateAsync(localUser);
            }

            return localUser;
        }

        public async Task<(bool Success, string Message, AppUser? User)> SearchAndImportUserAsync(string searchTerm, UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            var host = _config["Ldap:Host"];
            var portValue = _config["Ldap:Port"];
            if (!int.TryParse(portValue, out var port))
            {
                _logger.LogWarning("Invalid LDAP port value '{PortValue}'. Falling back to 389.", portValue);
                port = 389;
            }
            var bindDn = _config["Ldap:BindDn"];
            var bindPassword = _config["Ldap:BindPassword"];
            var searchBase = _config["Ldap:SearchBase"] ?? string.Empty;
            var userFilter = _config["Ldap:UserFilter"] ?? "(objectClass=user)";
            var defaultRole = _config["Ldap:DefaultRole"] ?? "User";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(bindDn) || string.IsNullOrWhiteSpace(bindPassword))
            {
                return (false, "LDAP no está configurado", null);
            }

            var useLdaps = bool.TryParse(_config["Ldap:UseLdaps"], out var ssl2) && ssl2;
            var ldapIdentifier = new LdapDirectoryIdentifier(host, port);
            using var conn = new LdapConnection(ldapIdentifier) { AuthType = AuthType.Basic };

            try
            {
                conn.SessionOptions.ProtocolVersion = 3;
                conn.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
                conn.SessionOptions.SecureSocketLayer = useLdaps;
                conn.Timeout = TimeSpan.FromSeconds(30);
                var cred = new NetworkCredential(bindDn, bindPassword);
                conn.Bind(cred);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error de conexión a LDAP");
                return (false, "Error al conectar con LDAP", null);
            }

            // Buscar usuario por sAMAccountName, mail o displayName
            var searchFilter = $"(&{userFilter}(|(sAMAccountName={searchTerm}*)(mail={searchTerm}*)(displayName=*{searchTerm}*)))";
            var request = new SearchRequest(searchBase, searchFilter, SearchScope.Subtree, new[] { AttrSamAccountName, AttrMail, AttrDisplayName, AttrUserAccountControl });
            
            SearchResponse? response;
            try
            {
                response = (SearchResponse)conn.SendRequest(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error buscando usuario {SearchTerm}", searchTerm);
                return (false, "Error al buscar en LDAP", null);
            }

            if (response.Entries.Count == 0)
            {
                return (false, "Usuario no encontrado en Active Directory", null);
            }

            var entry = response.Entries[0];
            var sam = entry.Attributes[AttrSamAccountName]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var mail = entry.Attributes[AttrMail]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var display = entry.Attributes[AttrDisplayName]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var uacStr = entry.Attributes[AttrUserAccountControl]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();

            bool enabled = true;
            if (int.TryParse(uacStr, out var uac)) enabled = (uac & 2) == 0;

            if (!enabled)
            {
                return (false, "La cuenta de usuario está deshabilitada en AD", null);
            }

            var externalId = sam ?? mail;
            if (string.IsNullOrWhiteSpace(externalId))
            {
                return (false, "No se pudo determinar el identificador del usuario", null);
            }

            // Buscar usuario local
            var localUser = await userManager.Users.FirstOrDefaultAsync(u => u.ExternalProvider == "LDAP" && u.ExternalId == externalId);
            if (localUser == null && !string.IsNullOrWhiteSpace(mail))
            {
                localUser = await userManager.FindByEmailAsync(mail);
            }

            if (localUser == null)
            {
                // Crear usuario nuevo
                var newUser = new AppUser
                {
                    UserName = !string.IsNullOrWhiteSpace(mail) ? mail : externalId,
                    Email = mail,
                    FullName = display ?? string.Empty,
                    ExternalProvider = "LDAP",
                    ExternalId = externalId,
                    IsActive = enabled,
                    CreatedAt = DateTime.UtcNow
                };

                var createResult = await userManager.CreateAsync(newUser);
                if (!createResult.Succeeded)
                {
                    return (false, $"Error al crear usuario: {string.Join(", ", createResult.Errors.Select(e => e.Description))}", null);
                }

                if (!await roleManager.RoleExistsAsync(defaultRole))
                {
                    await roleManager.CreateAsync(new IdentityRole(defaultRole));
                }

                await userManager.AddToRoleAsync(newUser, defaultRole);
                return (true, $"Usuario {externalId} importado correctamente", newUser);
            }
            else
            {
                // Actualizar usuario existente
                var changed = false;
                if (!string.IsNullOrWhiteSpace(mail) && localUser.Email != mail) { localUser.Email = mail; localUser.UserName = mail; changed = true; }
                if (!string.IsNullOrWhiteSpace(display) && localUser.FullName != display) { localUser.FullName = display; changed = true; }
                if (localUser.IsActive != enabled) { localUser.IsActive = enabled; changed = true; }
                if (changed) await userManager.UpdateAsync(localUser);
                
                return (true, $"Usuario {externalId} actualizado correctamente", localUser);
            }
        }
    }
}
