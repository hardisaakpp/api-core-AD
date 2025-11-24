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
    }
}
