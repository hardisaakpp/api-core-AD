using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCNexus.Core.API.Data;
using SCNexus.Core.API.Models;

namespace SCNexus.Core.API.Controllers
{
    /// <summary>
    /// Gestión de permisos (solo para Admin del Core)
    /// Los módulos NO acceden a estos endpoints, solo consultan el token
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class PermissionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;

        public PermissionsController(AppDbContext context, RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _roleManager = roleManager;
        }

        /// <summary>
        /// Listar todos los permisos
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPermissions()
        {
            var permissions = await _context.Permissions
                .OrderBy(p => p.Module)
                .ThenBy(p => p.Key)
                .ToListAsync();

            return Ok(permissions);
        }

        /// <summary>
        /// Crear un nuevo permiso
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionRequest request)
        {
            if (await _context.Permissions.AnyAsync(p => p.Key == request.Key))
                return BadRequest(new { message = "Permission key already exists." });

            var permission = new Permission
            {
                Key = request.Key,
                Description = request.Description,
                Module = request.Module
            };

            _context.Permissions.Add(permission);
            await _context.SaveChangesAsync();

            return Ok(permission);
        }

        /// <summary>
        /// Asignar un permiso a un rol
        /// </summary>
        [HttpPost("assign")]
        public async Task<IActionResult> AssignPermissionToRole([FromBody] AssignPermissionRequest request)
        {
            var role = await _roleManager.FindByNameAsync(request.RoleName);
            if (role == null)
                return NotFound(new { message = "Role not found." });

            var permission = await _context.Permissions.FindAsync(request.PermissionId);
            if (permission == null)
                return NotFound(new { message = "Permission not found." });

            var exists = await _context.RolePermissions
                .AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == request.PermissionId);

            if (exists)
                return BadRequest(new { message = "Permission already assigned to role." });

            _context.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = request.PermissionId
            });

            await _context.SaveChangesAsync();

            return Ok(new { message = "Permission assigned to role successfully." });
        }

        /// <summary>
        /// Listar permisos de un rol
        /// </summary>
        [HttpGet("role/{roleName}")]
        public async Task<IActionResult> GetRolePermissions(string roleName)
        {
            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null)
                return NotFound(new { message = "Role not found." });

            var permissions = await _context.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .Include(rp => rp.Permission)
                .Select(rp => rp.Permission)
                .ToListAsync();

            return Ok(permissions);
        }

        /// <summary>
        /// Remover un permiso de un rol
        /// </summary>
        [HttpDelete("unassign")]
        public async Task<IActionResult> UnassignPermissionFromRole([FromBody] AssignPermissionRequest request)
        {
            var role = await _roleManager.FindByNameAsync(request.RoleName);
            if (role == null)
                return NotFound(new { message = "Role not found." });

            var rolePermission = await _context.RolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == role.Id && rp.PermissionId == request.PermissionId);

            if (rolePermission == null)
                return NotFound(new { message = "Permission not assigned to role." });

            _context.RolePermissions.Remove(rolePermission);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Permission removed from role successfully." });
        }
    }

    public class CreatePermissionRequest
    {
        public string Key { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
    }

    public class AssignPermissionRequest
    {
        public string RoleName { get; set; } = string.Empty;
        public int PermissionId { get; set; }
    }
}
