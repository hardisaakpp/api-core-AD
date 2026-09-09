using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SCNexus.Core.API.Data;
using SCNexus.Core.API.Models;

namespace SCNexus.Core.API.Services
{
    public class AdLdapSyncService
    {
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
            var counters = new SyncCounters();

            LdapSyncResult Finish(bool success, string message) =>
                new(success, message, counters.Processed, counters.Created, counters.Updated, counters.Deactivated,
                    counters.Skipped, counters.Errors, startedAt, DateTimeOffset.UtcNow, trigger);

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
            var deactivateMissing = bool.TryParse(_config["Ldap:NightlySync:DeactivateMissing"], out var dm) && dm;
            var pageSize = int.TryParse(_config["Ldap:PageSize"], out var ps) && ps is > 0 and <= 1000 ? ps : 500;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(bindDn) || string.IsNullOrWhiteSpace(bindPassword))
            {
                _logger.LogWarning("LDAP config incomplete (Host/BindDn/BindPassword required)");
                return Finish(false, "LDAP no está configurado (Host/BindDn/BindPassword).");
            }

            var useLdaps = bool.TryParse(_config["Ldap:UseLdaps"], out var ssl) && ssl;
            LdapDirectoryIdentifier ldapIdentifier;
            try
            {
                ldapIdentifier = new LdapDirectoryIdentifier(host, port);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is TypeInitializationException)
            {
                _logger.LogError(ex, "LDAP native library not available (libldap). Cannot sync users.");
                return Finish(false, "Librería LDAP nativa no disponible en el servidor (libldap).");
            }

            var timeout = TimeSpan.FromMinutes(2);
            using var conn = new LdapConnection(ldapIdentifier) { AuthType = AuthType.Basic };
            try
            {
                conn.SessionOptions.ProtocolVersion = 3;
                conn.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
                conn.SessionOptions.SecureSocketLayer = useLdaps;
                conn.Timeout = timeout;
                conn.Bind(new NetworkCredential(bindDn, bindPassword));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bind to LDAP");
                return Finish(false, $"No se pudo conectar a Active Directory ({host}:{port}): {ex.Message}");
            }

            // Person accounts only: computers are objectClass=user too, and service/machine accounts
            // (sAMAccountName ending in $) must never become login users.
            var filter = $"(&{userFilter}(objectCategory=person)(!(sAMAccountName=*$)))";
            var attributes = new[] { "sAMAccountName", "mail", "displayName", "userAccountControl", "objectGUID" };
            var pageControl = new PageResultRequestControl(pageSize);
            var request = new SearchRequest(searchBase, filter, SearchScope.Subtree, attributes) { TimeLimit = timeout };
            request.Controls.Add(pageControl);

            var seenExternalIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pages = 0;

            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var response = (SearchResponse)conn.SendRequest(request, timeout);
                    pages++;

                    foreach (SearchResultEntry entry in response.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var outcome = await UpsertFromEntryAsync(entry, userManager, roleManager, defaultRole, seenExternalIds);
                        counters.Add(outcome);
                    }

                    var pageResponse = response.Controls.OfType<PageResultResponseControl>().FirstOrDefault();
                    if (pageResponse is null || pageResponse.Cookie.Length == 0)
                    {
                        break;
                    }

                    pageControl.Cookie = pageResponse.Cookie;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("LDAP sync cancelled after {Pages} page(s), {Processed} entries", pages, counters.Processed);
                return Finish(false, "Sincronización cancelada.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LDAP search failed after {Pages} page(s), {Processed} entries", pages, counters.Processed);
                return Finish(false, $"Error consultando Active Directory tras {counters.Processed} usuarios: {ex.Message}");
            }

            if (deactivateMissing && seenExternalIds.Count > 0)
            {
                counters.Deactivated = await DeactivateMissingAsync(userManager, seenExternalIds);
            }

            var summary = $"AD sync ({trigger}): {counters.Processed} procesados, {counters.Created} creados, {counters.Updated} actualizados, " +
                          $"{counters.Deactivated} desactivados, {counters.Skipped} omitidos, {counters.Errors} errores, {pages} página(s).";
            _logger.LogInformation("{Summary}", summary);
            return Finish(counters.Errors == 0, summary);
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
        private static readonly System.Text.RegularExpressions.Regex ValidUserName =
            new("^[A-Za-z0-9\\-._@+]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

        // AD attributes sometimes carry trailing spaces (typed by hand in the AD console); trimmed,
        // otherwise Identity rejects the user name and the account is skipped.
        private static string? Attr(SearchResultEntry entry, string name)
        {
            var value = entry.Attributes[name]?.GetValues(typeof(string)).FirstOrDefault()?.ToString()?.Trim();
            return string.IsNullOrEmpty(value) ? null : value;
        }

        private async Task<UpsertOutcome> UpsertFromEntryAsync(
            SearchResultEntry entry,
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            string defaultRole,
            HashSet<string> seenExternalIds)
        {
            try
            {
                var sam = Attr(entry, "sAMAccountName");
                var mail = Attr(entry, "mail");
                var display = Attr(entry, "displayName");
                var uacStr = Attr(entry, "userAccountControl");
                var enabled = !(int.TryParse(uacStr, out var uac) && (uac & 2) != 0); // UAC & 2 == ACCOUNTDISABLE

                var externalId = sam ?? mail;
                if (string.IsNullOrWhiteSpace(externalId))
                {
                    return UpsertOutcome.Skipped;
                }

                seenExternalIds.Add(externalId);

                var local = await userManager.Users.FirstOrDefaultAsync(u => u.ExternalProvider == "LDAP" && u.ExternalId == externalId);
                if (local == null && !string.IsNullOrWhiteSpace(mail))
                {
                    local = await userManager.FindByEmailAsync(mail);
                }
                if (local == null && !string.IsNullOrWhiteSpace(sam))
                {
                    // Users created by the login flow or by hand with the AD login as user name.
                    local = await userManager.FindByNameAsync(sam);
                }

                if (local == null)
                {
                    var userName = !string.IsNullOrWhiteSpace(mail) ? mail : externalId;
                    if (!ValidUserName.IsMatch(userName))
                    {
                        _logger.LogWarning("LDAP entry {Dn} skipped: user name '{UserName}' has characters Identity does not allow", entry.DistinguishedName, userName);
                        return UpsertOutcome.Skipped;
                    }

                    var newUser = new AppUser
                    {
                        UserName = userName,
                        Email = mail,
                        FullName = display ?? string.Empty,
                        ExternalProvider = "LDAP",
                        ExternalId = externalId,
                        IsActive = enabled,
                        CreatedAt = DateTime.UtcNow
                    };

                    var createRes = await userManager.CreateAsync(newUser);
                    if (!createRes.Succeeded)
                    {
                        _logger.LogWarning("LDAP entry {Dn} could not be created as '{UserName}': {Errors}", entry.DistinguishedName, userName,
                            string.Join("; ", createRes.Errors.Select(e => e.Description)));
                        return UpsertOutcome.Error;
                    }

                    if (!await roleManager.RoleExistsAsync(defaultRole)) await roleManager.CreateAsync(new IdentityRole(defaultRole));
                    await userManager.AddToRoleAsync(newUser, defaultRole);
                    return UpsertOutcome.Created;
                }

                var changed = false;
                if (!string.IsNullOrWhiteSpace(mail) && local.Email != mail) { local.Email = mail; local.UserName = mail; changed = true; }
                if (!string.IsNullOrWhiteSpace(display) && local.FullName != display) { local.FullName = display; changed = true; }
                if (local.IsActive != enabled) { local.IsActive = enabled; changed = true; }
                if (string.IsNullOrWhiteSpace(local.ExternalProvider)) { local.ExternalProvider = "LDAP"; changed = true; }
                if (string.IsNullOrWhiteSpace(local.ExternalId)) { local.ExternalId = externalId; changed = true; }

                if (!changed)
                {
                    return UpsertOutcome.Unchanged;
                }

                var updateRes = await userManager.UpdateAsync(local);
                if (!updateRes.Succeeded)
                {
                    _logger.LogWarning("LDAP entry {Dn} could not update local user {UserId}: {Errors}", entry.DistinguishedName, local.Id,
                        string.Join("; ", updateRes.Errors.Select(e => e.Description)));
                    return UpsertOutcome.Error;
                }

                return UpsertOutcome.Updated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing LDAP entry {Dn}", entry.DistinguishedName);
                return UpsertOutcome.Error;
            }
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
            var searchRequest = new SearchRequest(searchBase, searchFilter, SearchScope.Subtree, new[] { "sAMAccountName", "mail", "displayName", "userPrincipalName", "distinguishedName", "userAccountControl" });
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
            var sam = entry.Attributes["sAMAccountName"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var mail = entry.Attributes["mail"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var display = entry.Attributes["displayName"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var upn = entry.Attributes["userPrincipalName"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var uacStr = entry.Attributes["userAccountControl"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            
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
            var request = new SearchRequest(searchBase, searchFilter, SearchScope.Subtree, new[] { "sAMAccountName", "mail", "displayName", "userAccountControl" });
            
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
            var sam = entry.Attributes["sAMAccountName"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var mail = entry.Attributes["mail"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var display = entry.Attributes["displayName"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
            var uacStr = entry.Attributes["userAccountControl"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();

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
