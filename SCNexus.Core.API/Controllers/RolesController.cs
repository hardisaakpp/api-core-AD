using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCNexus.Core.API.Data;
using SCNexus.Core.API.Models;

namespace SCNexus.Core.API.Controllers
{
    /// <summary>
    /// CRUD de roles. Los permisos de cada rol se gestionan en <see cref="PermissionsController"/> (assign / unassign).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public partial class RolesController : ControllerBase
    {
        /// <summary>
        /// Roles que el Core recrea al arrancar (DataSeeder) o que el código usa por nombre ("Admin" protege los endpoints).
        /// Renombrarlos o eliminarlos rompería el acceso o sería deshecho en el siguiente arranque.
        /// </summary>
        public static readonly IReadOnlySet<string> SystemRoles =
            new HashSet<string>(["Admin", "User", "Contabilidad", "Gerente"], StringComparer.OrdinalIgnoreCase);

        public const int MaxNameLength = 50;

        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly AppDbContext _context;

        public RolesController(RoleManager<IdentityRole> roleManager, AppDbContext context)
        {
            _roleManager = roleManager;
            _context = context;
        }

        [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} _.\-]*$")]
        private static partial Regex NameRegex();

        /// <summary>Valida y normaliza nombre y descripción; devuelve el mensaje de error o null si todo está bien.</summary>
        public static string? Validate(string? name, string? description, out string cleanName, out string cleanDescription)
        {
            cleanName = (name ?? string.Empty).Trim();
            cleanDescription = (description ?? string.Empty).Trim();
            if (cleanName.Length < 2 || cleanName.Length > MaxNameLength)
                return $"El nombre del rol debe tener entre 2 y {MaxNameLength} caracteres.";
            if (!NameRegex().IsMatch(cleanName))
                return "El nombre del rol solo puede llevar letras, números, espacios, guion, guion bajo y punto, y debe empezar por letra o número.";
            if (cleanDescription.Length > RoleDescription.MaxLength)
                return $"La descripción no puede superar {RoleDescription.MaxLength} caracteres.";
            return null;
        }

        private async Task<List<RoleResponse>> BuildResponsesAsync(IQueryable<IdentityRole> roles)
        {
            var list = await roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
            var ids = list.Select(r => r.Id).ToList();
            var users = await _context.UserRoles.Where(ur => ids.Contains(ur.RoleId))
                .GroupBy(ur => ur.RoleId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
            var perms = await _context.RolePermissions.Where(rp => ids.Contains(rp.RoleId))
                .GroupBy(rp => rp.RoleId).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
            var descriptions = await _context.RoleDescriptions.Where(d => ids.Contains(d.RoleId))
                .ToDictionaryAsync(d => d.RoleId, d => d.Description);

            return list.Select(r => new RoleResponse
            {
                Id = r.Id,
                Name = r.Name ?? string.Empty,
                NormalizedName = r.NormalizedName,
                Description = descriptions.GetValueOrDefault(r.Id, string.Empty),
                UsersCount = users.GetValueOrDefault(r.Id),
                PermissionsCount = perms.GetValueOrDefault(r.Id),
                IsSystem = SystemRoles.Contains(r.Name ?? string.Empty)
            }).ToList();
        }

        // GET: api/Roles
        [HttpGet]
        public async Task<ActionResult<IEnumerable<RoleResponse>>> GetAll()
            => Ok(await BuildResponsesAsync(_roleManager.Roles));

        // GET: api/Roles/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<RoleResponse>> GetById(string id)
        {
            var found = (await BuildResponsesAsync(_roleManager.Roles.Where(r => r.Id == id))).FirstOrDefault();
            return found is null ? NotFound(new { message = "Rol no encontrado." }) : Ok(found);
        }

        // POST: api/Roles
        [HttpPost]
        public async Task<ActionResult<RoleResponse>> Create([FromBody] CreateRoleRequest request)
        {
            var error = Validate(request.Name, request.Description, out var name, out var description);
            if (error != null) return BadRequest(new { message = error });

            if (await _roleManager.RoleExistsAsync(name))
                return Conflict(new { message = $"Ya existe un rol llamado \"{name}\"." });

            var role = new IdentityRole(name);
            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });

            if (description.Length > 0)
            {
                _context.RoleDescriptions.Add(new RoleDescription { RoleId = role.Id, Description = description });
                await _context.SaveChangesAsync();
            }

            var created = (await BuildResponsesAsync(_roleManager.Roles.Where(r => r.Id == role.Id))).First();
            return CreatedAtAction(nameof(GetById), new { id = role.Id }, created);
        }

        // PUT: api/Roles/{id}
        [HttpPut("{id}")]
        public async Task<ActionResult<RoleResponse>> Update(string id, [FromBody] UpdateRoleRequest request)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null) return NotFound(new { message = "Rol no encontrado." });

            var error = Validate(request.Name, request.Description, out var name, out var description);
            if (error != null) return BadRequest(new { message = error });

            var renaming = !string.Equals(role.Name, name, StringComparison.Ordinal);
            if (renaming)
            {
                if (SystemRoles.Contains(role.Name ?? string.Empty))
                    return BadRequest(new { message = $"\"{role.Name}\" es un rol del sistema y no se puede renombrar. Sí puedes cambiar su descripción." });

                var other = await _roleManager.FindByNameAsync(name);
                if (other is not null && other.Id != role.Id)
                    return Conflict(new { message = $"Ya existe un rol llamado \"{name}\"." });

                var renamed = await _roleManager.SetRoleNameAsync(role, name);
                if (renamed.Succeeded) renamed = await _roleManager.UpdateAsync(role);
                if (!renamed.Succeeded)
                    return BadRequest(new { message = string.Join(" ", renamed.Errors.Select(e => e.Description)) });
            }

            var existing = await _context.RoleDescriptions.FindAsync(role.Id);
            if (description.Length == 0)
            {
                if (existing is not null) _context.RoleDescriptions.Remove(existing);
            }
            else if (existing is null)
            {
                _context.RoleDescriptions.Add(new RoleDescription { RoleId = role.Id, Description = description });
            }
            else
            {
                existing.Description = description;
            }
            await _context.SaveChangesAsync();

            return Ok((await BuildResponsesAsync(_roleManager.Roles.Where(r => r.Id == role.Id))).First());
        }

        // DELETE: api/Roles/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null) return NotFound(new { message = "Rol no encontrado." });

            if (SystemRoles.Contains(role.Name ?? string.Empty))
                return BadRequest(new { message = $"\"{role.Name}\" es un rol del sistema y no se puede eliminar." });

            var users = await _context.UserRoles.CountAsync(ur => ur.RoleId == role.Id);
            if (users > 0)
                return Conflict(new { message = $"No se puede eliminar \"{role.Name}\": {users} usuario(s) lo tienen asignado. Quita el rol a esos usuarios primero." });

            // Los permisos y la descripción del rol se eliminan en cascada (FK), pero se hace explícito para no depender de ello.
            _context.RolePermissions.RemoveRange(_context.RolePermissions.Where(rp => rp.RoleId == role.Id));
            _context.RoleDescriptions.RemoveRange(_context.RoleDescriptions.Where(d => d.RoleId == role.Id));
            await _context.SaveChangesAsync();

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded)
                return BadRequest(new { message = string.Join(" ", result.Errors.Select(e => e.Description)) });
            return NoContent();
        }
    }
}
