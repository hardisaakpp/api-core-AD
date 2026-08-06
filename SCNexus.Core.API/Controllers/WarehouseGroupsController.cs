using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SCNexus.Core.API.Data;
using SCNexus.Core.API.Models;
using System.Security.Claims;

namespace SCNexus.Core.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class WarehouseGroupsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WarehouseGroupsController(AppDbContext context)
        {
            _context = context;
        }

        // Lectura abierta a cualquier usuario autenticado: otros servicios (ej. Caja Chica)
        // necesitan resolver a que grupo pertenece un almacen sin ser Admin.
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var items = await _context.WarehouseGroups
                .OrderBy(g => g.Grupo)
                .Select(g => new
                {
                    id = g.Id,
                    grupo = g.Grupo,
                    descripcion = g.Descripcion,
                    almacenes = g.Almacenes,
                    bodegaCentral = g.BodegaCentral,
                    fechaCreacion = g.FechaCreacion,
                    fechaModificacion = g.FechaModificacion,
                    modificadoPor = g.ModificadoPor
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string grupo)
        {
            if (string.IsNullOrWhiteSpace(grupo))
                return BadRequest(new { message = "El parámetro 'grupo' es obligatorio." });

            var items = await _context.WarehouseGroups
                .Where(g => g.Grupo.ToLower().Contains(grupo.ToLower()))
                .OrderBy(g => g.Grupo)
                .Select(g => new
                {
                    id = g.Id,
                    grupo = g.Grupo,
                    descripcion = g.Descripcion,
                    almacenes = g.Almacenes,
                    bodegaCentral = g.BodegaCentral,
                    fechaCreacion = g.FechaCreacion,
                    fechaModificacion = g.FechaModificacion,
                    modificadoPor = g.ModificadoPor
                })
                .ToListAsync();

            return Ok(items);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var grupo = await _context.WarehouseGroups
                .Where(g => g.Id == id)
                .Select(g => new
                {
                    id = g.Id,
                    grupo = g.Grupo,
                    descripcion = g.Descripcion,
                    almacenes = g.Almacenes,
                    bodegaCentral = g.BodegaCentral,
                    fechaCreacion = g.FechaCreacion,
                    fechaModificacion = g.FechaModificacion,
                    modificadoPor = g.ModificadoPor
                })
                .FirstOrDefaultAsync();

            if (grupo == null)
                return NotFound(new { message = "Grupo no encontrado." });

            return Ok(grupo);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([FromBody] WarehouseGroupRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Grupo))
                return BadRequest(new { message = "El nombre del grupo es obligatorio." });

            if (request.Almacenes == null || request.Almacenes.Count == 0)
                return BadRequest(new { message = "Debes agregar al menos un almacén." });

            var nombreTrim = request.Grupo.Trim();
            var duplicate = await _context.WarehouseGroups
                .AnyAsync(g => g.Grupo.ToLower() == nombreTrim.ToLower());
            if (duplicate)
                return BadRequest(new { message = $"Ya existe un grupo con el nombre '{nombreTrim}'." });

            var caller = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email);

            var grupo = new WarehouseGroup
            {
                Grupo = nombreTrim,
                Descripcion = request.Descripcion?.Trim(),
                Almacenes = request.Almacenes
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Select(c => c.Trim().ToUpper())
                    .Distinct()
                    .ToList(),
                BodegaCentral = string.IsNullOrWhiteSpace(request.BodegaCentral) ? null : request.BodegaCentral.Trim().ToUpper(),
                FechaCreacion = DateTime.UtcNow,
                ModificadoPor = caller
            };

            _context.WarehouseGroups.Add(grupo);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = grupo.Id,
                grupo = grupo.Grupo,
                descripcion = grupo.Descripcion,
                almacenes = grupo.Almacenes,
                bodegaCentral = grupo.BodegaCentral,
                fechaCreacion = grupo.FechaCreacion,
                modificadoPor = grupo.ModificadoPor
            });
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, [FromBody] WarehouseGroupRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Grupo))
                return BadRequest(new { message = "El nombre del grupo es obligatorio." });

            if (request.Almacenes == null || request.Almacenes.Count == 0)
                return BadRequest(new { message = "Debes agregar al menos un almacén." });

            var nombreTrimUpd = request.Grupo.Trim();
            var duplicateUpd = await _context.WarehouseGroups
                .AnyAsync(g => g.Id != id && g.Grupo.ToLower() == nombreTrimUpd.ToLower());
            if (duplicateUpd)
                return BadRequest(new { message = $"Ya existe un grupo con el nombre '{nombreTrimUpd}'." });

            var grupo = await _context.WarehouseGroups.FindAsync(id);
            if (grupo == null)
                return NotFound(new { message = "Grupo no encontrado." });

            var caller = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email);

            grupo.Grupo = nombreTrimUpd;
            grupo.Descripcion = request.Descripcion?.Trim();
            grupo.Almacenes = request.Almacenes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToUpper())
                .Distinct()
                .ToList();
            grupo.BodegaCentral = string.IsNullOrWhiteSpace(request.BodegaCentral) ? null : request.BodegaCentral.Trim().ToUpper();
            grupo.FechaModificacion = DateTime.UtcNow;
            grupo.ModificadoPor = caller;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = grupo.Id,
                grupo = grupo.Grupo,
                descripcion = grupo.Descripcion,
                almacenes = grupo.Almacenes,
                bodegaCentral = grupo.BodegaCentral,
                fechaCreacion = grupo.FechaCreacion,
                fechaModificacion = grupo.FechaModificacion,
                modificadoPor = grupo.ModificadoPor
            });
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var grupo = await _context.WarehouseGroups.FindAsync(id);
            if (grupo == null)
                return NotFound(new { message = "Grupo no encontrado." });

            var accesos = await _context.UserWarehouseAccesses.Where(a => a.WarehouseGroupId == id).ToListAsync();
            foreach (var acceso in accesos)
                acceso.WarehouseGroupId = null;

            _context.WarehouseGroups.Remove(grupo);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Grupo eliminado correctamente." });
        }
    }

    public class WarehouseGroupRequest
    {
        public string Grupo { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public List<string> Almacenes { get; set; } = [];
        public string? BodegaCentral { get; set; }
    }
}
