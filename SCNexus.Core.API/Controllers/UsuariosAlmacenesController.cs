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
            await using var cmd = new SqlCommand("SELECT cod_almacen, nombre FROM dbo.almacenes ORDER BY cod_almacen", conn);
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

            var duplicate = await _context.UserWarehouseAccesses
                .AnyAsync(a => a.UserId == request.UserId
                            && a.CodAlmacen == codAlmacen
                            && a.WarehouseGroupId == request.WarehouseGroupId);
            if (duplicate)
                return BadRequest(new { message = "El usuario ya tiene ese acceso con ese grupo de almacén." });

            var caller = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email);

            var entry = new AspNetUserWarehouseAccess
            {
                UserId = request.UserId,
                CodAlmacen = codAlmacen,
                AccesoAlmacen = request.AccesoAlmacen.Trim(),
                WarehouseGroupId = request.WarehouseGroupId,
                FechaCreacion = DateTime.UtcNow,
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

            var caller = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email);
            entry.AccesoAlmacen = request.AccesoAlmacen.Trim();
            entry.WarehouseGroupId = request.WarehouseGroupId;
            entry.ModificadoPor = caller;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Asignación actualizada correctamente." });
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
            await using var cmd = new SqlCommand("SELECT COUNT(1) FROM dbo.almacenes WHERE cod_almacen = @cod", conn);
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
        public string AccesoAlmacen { get; set; } = string.Empty;
        public Guid? WarehouseGroupId { get; set; }
    }
}
