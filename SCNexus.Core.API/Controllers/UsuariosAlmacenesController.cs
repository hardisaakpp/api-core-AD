using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public UsuariosAlmacenesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _context.UserWarehouseAccesses
                .Include(a => a.User)
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
                .Where(a => a.UserId == userId)
                .OrderBy(a => a.CodAlmacen)
                .Select(a => new
                {
                    id = a.Id,
                    userId = a.UserId,
                    codAlmacen = a.CodAlmacen,
                    accesoAlmacen = a.AccesoAlmacen,
                    fechaCreacion = a.FechaCreacion,
                    modificadoPor = a.ModificadoPor
                })
                .ToListAsync();

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
                .AnyAsync(a => a.UserId == request.UserId && a.CodAlmacen == codAlmacen);
            if (duplicate)
                return BadRequest(new { message = "El usuario ya tiene acceso a ese almacén." });

            var caller = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email);

            var entry = new AspNetUserWarehouseAccess
            {
                UserId = request.UserId,
                CodAlmacen = codAlmacen,
                AccesoAlmacen = request.AccesoAlmacen.Trim(),
                FechaCreacion = DateTime.UtcNow,
                ModificadoPor = caller
            };

            _context.UserWarehouseAccesses.Add(entry);
            await _context.SaveChangesAsync();

            return Ok(new { id = entry.Id, message = "Asignación creada correctamente." });
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
            var connection = _context.Database.GetDbConnection();
            var wasOpen = connection.State == ConnectionState.Open;
            try
            {
                if (!wasOpen) await connection.OpenAsync();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(1) FROM SCMaestros.dbo.almacenes WHERE cod_almacen = @cod";
                var param = cmd.CreateParameter();
                param.ParameterName = "@cod";
                param.Value = codAlmacen;
                cmd.Parameters.Add(param);
                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            finally
            {
                if (!wasOpen) connection.Close();
            }
        }
    }

    public class CreateUsuarioAlmacenRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string CodAlmacen { get; set; } = string.Empty;
        public string AccesoAlmacen { get; set; } = string.Empty;
    }
}
