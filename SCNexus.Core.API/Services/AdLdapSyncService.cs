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

        public async Task<int> SyncUsersAsync(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext db)
        {
            var host = _config["Ldap:Host"];
            var port = int.Parse(_config["Ldap:Port"] ?? "389");
            var bindDn = _config["Ldap:BindDn"];
            var bindPassword = _config["Ldap:BindPassword"];
            var searchBase = _config["Ldap:SearchBase"] ?? string.Empty;
            var userFilter = _config["Ldap:UserFilter"] ?? "(objectClass=user)";
            var defaultRole = _config["Ldap:DefaultRole"] ?? "User";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(bindDn) || string.IsNullOrWhiteSpace(bindPassword))
            {
                _logger.LogWarning("LDAP config incomplete (Host/BindDn/BindPassword required)");
                return 0;
            }

                var useLdaps = bool.TryParse(_config["Ldap:UseLdaps"], out var ssl) && ssl;
                var ldapIdentifier = new LdapDirectoryIdentifier(host, port);
                using var conn = new LdapConnection(ldapIdentifier)
                {
                    AuthType = AuthType.Basic
                };
            try
            {
                conn.SessionOptions.ProtocolVersion = 3;
                conn.Timeout = TimeSpan.FromSeconds(30);
                var cred = new NetworkCredential(bindDn, bindPassword);
                conn.Bind(cred);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to bind to LDAP");
                throw;
            }

            var request = new SearchRequest(searchBase, userFilter, SearchScope.Subtree, new[] { "sAMAccountName", "mail", "displayName", "userAccountControl", "objectGUID" });
            var response = (SearchResponse)conn.SendRequest(request);
            var processed = 0;

            foreach (SearchResultEntry entry in response.Entries)
            {
                try
                {
                    var sam = entry.Attributes["sAMAccountName"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
                    var mail = entry.Attributes["mail"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
                    var display = entry.Attributes["displayName"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
                    var uacStr = entry.Attributes["userAccountControl"]?.GetValues(typeof(string)).FirstOrDefault()?.ToString();
                    bool enabled = true;
                    if (int.TryParse(uacStr, out var uac)) enabled = (uac & 2) == 0; // UAC & 2 == ACCOUNTDISABLE

                    var externalId = sam ?? mail;
                    if (string.IsNullOrWhiteSpace(externalId)) continue;

                    var local = await userManager.Users.FirstOrDefaultAsync(u => u.ExternalProvider == "LDAP" && u.ExternalId == externalId);
                    if (local == null && !string.IsNullOrWhiteSpace(mail))
                    {
                        local = await userManager.FindByEmailAsync(mail);
                    }

                    if (local == null)
                    {
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

                        var createRes = await userManager.CreateAsync(newUser);
                        if (createRes.Succeeded)
                        {
                            if (!await roleManager.RoleExistsAsync(defaultRole)) await roleManager.CreateAsync(new IdentityRole(defaultRole));
                            await userManager.AddToRoleAsync(newUser, defaultRole);
                        }
                    }
                    else
                    {
                        var changed = false;
                        if (!string.IsNullOrWhiteSpace(mail) && local.Email != mail) { local.Email = mail; local.UserName = mail; changed = true; }
                        if (!string.IsNullOrWhiteSpace(display) && local.FullName != display) { local.FullName = display; changed = true; }
                        if (local.IsActive != enabled) { local.IsActive = enabled; changed = true; }
                        if (string.IsNullOrWhiteSpace(local.ExternalProvider)) { local.ExternalProvider = "LDAP"; changed = true; }
                        if (string.IsNullOrWhiteSpace(local.ExternalId)) { local.ExternalId = externalId; changed = true; }
                        if (changed) await userManager.UpdateAsync(local);
                    }

                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing LDAP entry {Dn}", entry.DistinguishedName);
                }
            }

            // connection disposed by using
            _logger.LogInformation("LDAP sync processed {Count} entries", processed);
            return processed;
        }

        public async Task<AppUser?> AuthenticateLdapUserAsync(string userName, string password, UserManager<AppUser> userManager)
        {
            var host = _config["Ldap:Host"];
            var port = int.Parse(_config["Ldap:Port"] ?? "389");
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
            var ldapIdentifier = new LdapDirectoryIdentifier(host, port);
            using var conn = new LdapConnection(ldapIdentifier)
            {
                AuthType = AuthType.Basic
            };
            
            try
            {
                conn.SessionOptions.ProtocolVersion = 3;
                conn.Timeout = TimeSpan.FromSeconds(30);
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
            searchRequest.TimeLimit = TimeSpan.FromSeconds(30);
            
            SearchResponse? searchResponse = null;
            try
            {
                searchResponse = (SearchResponse)conn.SendRequest(searchRequest);
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
            var port = int.Parse(_config["Ldap:Port"] ?? "389");
            var bindDn = _config["Ldap:BindDn"];
            var bindPassword = _config["Ldap:BindPassword"];
            var searchBase = _config["Ldap:SearchBase"] ?? string.Empty;
            var userFilter = _config["Ldap:UserFilter"] ?? "(objectClass=user)";
            var defaultRole = _config["Ldap:DefaultRole"] ?? "User";

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(bindDn) || string.IsNullOrWhiteSpace(bindPassword))
            {
                return (false, "LDAP no está configurado", null);
            }

            var ldapIdentifier = new LdapDirectoryIdentifier(host, port);
            using var conn = new LdapConnection(ldapIdentifier) { AuthType = AuthType.Basic };

            try
            {
                conn.SessionOptions.ProtocolVersion = 3;
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
