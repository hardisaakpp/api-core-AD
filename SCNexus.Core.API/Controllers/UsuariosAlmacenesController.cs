using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SCNexus.Core.API.Data;
using SCNexus.Core.API.Models;
using System.Data;
using System.Security.Claims;

namespace SCNexus.Core.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsuariosAlmacenesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public UsuariosAlmacenesController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _context.UserWarehouseAccesses
                .Include(a => a.User)
                .Include(a => a.WarehouseGroup)
                .OrderBy(a => a.User.UserName)
                .ThenBy(a => a.CodAlmacen)
                .Select(a => new
                {
                    id = a.Id,
                    userId = a.UserId,
                    userName = a.User.UserName,
                    fullName = a.User.FullName,
                    codAlmacen = a.CodAlmacen,
                    accesoAlmacen = a.AccesoAlmacen,
                    grupoId = a.WarehouseGroupId,
                    grupoNombre = a.WarehouseGroup != null ? a.WarehouseGroup.Grupo : null,
                    fechaCreacion = a.FechaCreacion,
                    fechaModificacion = a.FechaModificacion,
                    modificadoPor = a.ModificadoPor
                })
                .ToListAsync();

            return Ok(items);
        }

        // Accessible to the owner or Admin — avoids fetching all records client-side
        [HttpGet("user/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetByUser(string userId)
        {
            var requestingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            if (!isAdmin && requestingUserId != userId)
                return Forbid();

            var items = await _context.UserWarehouseAccesses
                .Include(a => a.WarehouseGroup)
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.CodAlmacen)
                .Select(a => new
                {
                    id = a.Id,
                    userId = a.UserId,
                    codAlmacen = a.CodAlmacen,
                    accesoAlmacen = a.AccesoAlmacen,
                    grupoId = a.WarehouseGroupId,
                    grupoNombre = a.WarehouseGroup != null ? a.WarehouseGroup.Grupo : null,
                    fechaCreacion = a.FechaCreacion,
                    fechaModificacion = a.FechaModificacion,
                    modificadoPor = a.ModificadoPor
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("almacenes")]
        public async Task<IActionResult> GetAlmacenes()
        {
            var cs = _config.GetConnectionString("SCMaestrosConnection");
            var items = new List<object>();
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT whs_code, whs_name FROM dbo.almacenes_local ORDER BY whs_code", conn);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(new
                {
                    codAlmacen = reader.GetString(0),
                    nombre = reader.IsDBNull(1) ? null : reader.GetString(1)
                });
            }
            return Ok(items);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUsuarioAlmacenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.UserId) ||
                string.IsNullOrWhiteSpace(request.CodAlmacen) ||
                string.IsNullOrWhiteSpace(request.AccesoAlmacen))
                return BadRequest(new { message = "UserId, CodAlmacen y AccesoAlmacen son requeridos." });

            var userExists = await _context.Users.AnyAsync(u => u.Id == request.UserId);
            if (!userExists)
                return NotFound(new { message = "Usuario no encontrado." });

            var codAlmacen = request.CodAlmacen.Trim().ToUpper();

            var almacenValido = await AlmacenExisteEnScMaestros(codAlmacen);
            if (!almacenValido)
                return BadRequest(new { message = $"El código de almacén '{codAlmacen}' no existe en el maestro de almacenes (SCMaestros)." });

            var otroAlmacen = await _context.UserWarehouseAccesses
                .Where(a => a.UserId == request.UserId)
                .Select(a => a.CodAlmacen)
                .FirstOrDefaultAsync();
            if (otroAlmacen != null && otroAlmacen != codAlmacen)
                return BadRequest(new { message = $"El usuario ya tiene asignado el almacén '{otroAlmacen}'. Un usuario solo puede tener un almacén." });

            var duplicate = await _context.UserWarehouseAccesses
                .AnyAsync(a => a.UserId == request.UserId
                            && a.CodAlmacen == codAlmacen
                            && a.WarehouseGroupId == request.WarehouseGroupId);
            if (duplicate)
                return BadRequest(new { message = "El usuario ya tiene ese acceso con ese grupo de almacén." });

            var caller = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email);

            var now = DateTime.UtcNow;
            var entry = new AspNetUserWarehouseAccess
            {
                UserId = request.UserId,
                CodAlmacen = codAlmacen,
                AccesoAlmacen = request.AccesoAlmacen.Trim(),
                WarehouseGroupId = request.WarehouseGroupId,
                FechaCreacion = now,
                FechaModificacion = now,
                ModificadoPor = caller
            };

            _context.UserWarehouseAccesses.Add(entry);
            await _context.SaveChangesAsync();

            return Ok(new { id = entry.Id, message = "Asignación creada correctamente." });
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUsuarioAlmacenRequest request)
        {
            var entry = await _context.UserWarehouseAccesses.FindAsync(id);
            if (entry == null)
                return NotFound(new { message = "Asignación no encontrada." });

            if (string.IsNullOrWhiteSpace(request.AccesoAlmacen))
                return BadRequest(new { message = "AccesoAlmacen es requerido." });

            var newUserId = string.IsNullOrWhiteSpace(request.UserId) ? entry.UserId : request.UserId.Trim();
            var newCodAlmacen = string.IsNullOrWhiteSpace(request.CodAlmacen) ? entry.CodAlmacen : request.CodAlmacen.Trim().ToUpper();

            var validationError = await ValidateUpdateAsync(id, entry, request, newUserId, newCodAlmacen);
            if (validationError != null)
                return validationError;

            var caller = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email);
            entry.UserId = newUserId;
            entry.CodAlmacen = newCodAlmacen;
            entry.AccesoAlmacen = request.AccesoAlmacen.Trim();
            entry.WarehouseGroupId = request.WarehouseGroupId;
            entry.FechaModificacion = DateTime.UtcNow;
            entry.ModificadoPor = caller;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Asignación actualizada correctamente." });
        }

        private async Task<IActionResult?> ValidateUpdateAsync(Guid id, AspNetUserWarehouseAccess entry, UpdateUsuarioAlmacenRequest request, string newUserId, string newCodAlmacen)
        {
            if (newUserId != entry.UserId)
            {
                var userExists = await _context.Users.AnyAsync(u => u.Id == newUserId);
                if (!userExists)
                    return NotFound(new { message = "Usuario no encontrado." });
            }

            if (newCodAlmacen != entry.CodAlmacen)
            {
                var almacenValido = await AlmacenExisteEnScMaestros(newCodAlmacen);
                if (!almacenValido)
                    return BadRequest(new { message = $"El código de almacén '{newCodAlmacen}' no existe en el maestro de almacenes (SCMaestros)." });
            }

            var userOAlmacenCambio = newUserId != entry.UserId || newCodAlmacen != entry.CodAlmacen;
            if (userOAlmacenCambio)
            {
                var otroAlmacen = await _context.UserWarehouseAccesses
                    .Where(a => a.Id != id && a.UserId == newUserId)
                    .Select(a => a.CodAlmacen)
                    .FirstOrDefaultAsync();
                if (otroAlmacen != null && otroAlmacen != newCodAlmacen)
                    return BadRequest(new { message = $"Ese usuario ya tiene asignado el almacén '{otroAlmacen}'. Un usuario solo puede tener un almacén." });
            }

            if (userOAlmacenCambio || request.WarehouseGroupId != entry.WarehouseGroupId)
            {
                var duplicate = await _context.UserWarehouseAccesses
                    .AnyAsync(a => a.Id != id
                                && a.UserId == newUserId
                                && a.CodAlmacen == newCodAlmacen
                                && a.WarehouseGroupId == request.WarehouseGroupId);
                if (duplicate)
                    return BadRequest(new { message = "Ya existe una asignación con ese usuario, almacén y grupo." });
            }

            return null;
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var entry = await _context.UserWarehouseAccesses.FindAsync(id);
            if (entry == null)
                return NotFound(new { message = "Asignación no encontrada." });

            _context.UserWarehouseAccesses.Remove(entry);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Asignación eliminada correctamente." });
        }

        private async Task<bool> AlmacenExisteEnScMaestros(string codAlmacen)
        {
            var cs = _config.GetConnectionString("SCMaestrosConnection");
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync();
            await using var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.almacenes_local WHERE whs_code = @cod", conn);
            cmd.Parameters.AddWithValue("@cod", codAlmacen);
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
    }

    public class CreateUsuarioAlmacenRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string CodAlmacen { get; set; } = string.Empty;
        public string AccesoAlmacen { get; set; } = string.Empty;
        public Guid? WarehouseGroupId { get; set; }
    }

    public class UpdateUsuarioAlmacenRequest
    {
        public string? UserId { get; set; }
        public string? CodAlmacen { get; set; }
        public string AccesoAlmacen { get; set; } = string.Empty;
        public Guid? WarehouseGroupId { get; set; }
    }
}
