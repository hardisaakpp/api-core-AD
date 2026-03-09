using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SCNexus.Core.API.Models;
using SCNexus.Core.API.Services;
using SCNexus.Core.API.Data;

namespace SCNexus.Core.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly AdLdapSyncService _syncService;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _db;

        public AdminController(AdLdapSyncService syncService, UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext db)
        {
            _syncService = syncService;
            _userManager = userManager;
            _roleManager = roleManager;
            _db = db;
        }

        [HttpPost("sync-ldap")]
        public async Task<IActionResult> SyncLdap()
        {
            var count = await _syncService.SyncUsersAsync(_userManager, _roleManager, _db);
            return Ok(new { processed = count });
        }

        [HttpPost("search-and-import-user")]
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
