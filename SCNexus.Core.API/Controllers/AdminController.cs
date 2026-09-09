using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SCNexus.Core.API.Models;
using SCNexus.Core.API.Services;

namespace SCNexus.Core.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        /// <summary>
        /// Obtiene el nombre completo de un usuario local (no AD) por su ID.
        /// GET: /api/Admin/user-local-name/{id}
        /// </summary>
        [HttpGet("user-local-name/{id}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLocalUserNameById(string id)
        {
            // Buscar usuario por ID
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "Usuario no encontrado" });
            // Devuelve el nombre de cualquier usuario (local o AD)
            return Ok(new { id = user.Id, fullName = user.FullName });
        }
        private readonly AdLdapSyncService _syncService;
        private readonly LdapSyncRunner _syncRunner;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public AdminController(AdLdapSyncService syncService, LdapSyncRunner syncRunner, UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _syncService = syncService;
            _syncRunner = syncRunner;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        /// <summary>
        /// Starts a full AD synchronisation in the background and returns 202 with the current
        /// status; poll GET sync-ldap/status for the result. Running it inline used to time out at
        /// the gateway on large directories. 409 when a run is already in progress.
        /// </summary>
        [HttpPost("sync-ldap")]
        [Authorize(Roles = "Admin")]
        public IActionResult SyncLdap()
        {
            var trigger = $"manual:{User.Identity?.Name ?? "admin"}";
            if (!_syncRunner.TryStartInBackground(trigger))
            {
                return Conflict(new { message = "Ya hay una sincronización de AD en curso.", status = _syncRunner.Status });
            }

            return Accepted(new { message = "Sincronización de AD iniciada.", status = _syncRunner.Status });
        }

        /// <summary>Last result, whether a run is in progress and when the nightly job fires next.</summary>
        [HttpGet("sync-ldap/status")]
        [Authorize(Roles = "Admin")]
        public IActionResult SyncLdapStatus() => Ok(_syncRunner.Status);

        [HttpPost("search-and-import-user")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SearchAndImportUser([FromBody] SearchUserRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                return BadRequest(new { message = "El término de búsqueda es requerido" });
            }

            var (success, message, user) = await _syncService.SearchAndImportUserAsync(request.SearchTerm, _userManager, _roleManager);
            
            if (success && user != null)
            {
                return Ok(new { 
                    success = true, 
                    message,
                    user = new { 
                        id = user.Id, 
                        userName = user.UserName, 
                        email = user.Email, 
                        fullName = user.FullName 
                    }
                });
            }

            return BadRequest(new { success = false, message });
        }
    }

    public class SearchUserRequest
    {
        public string SearchTerm { get; set; } = string.Empty;
    }
}
