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
    [Authorize] // Solo usuarios autenticados
    public class AuditLogsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<AuditLogsController> _logger;

        public AuditLogsController(AppDbContext context, ILogger<AuditLogsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todos los registros de auditoría (solo para administradores idealmente)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var logs = await _context.AuditLogs
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync();

                return Ok(logs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo auditorías");
                return StatusCode(500, "Error interno al obtener auditorías");
            }
        }

        /// <summary>
        /// Crea un nuevo registro de auditoría
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AuditLog log)
        {
            if (log == null)
                return BadRequest("Datos inválidos");

            try
            {
                // Asignar el usuario autenticado si no se envió
                log.UserName ??= User.FindFirstValue(ClaimTypes.Name) ?? "system";
                log.CreatedAt = DateTime.UtcNow;

                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetAll), new { id = log.Id }, log);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando auditoría");
                return StatusCode(500, "Error interno al guardar auditoría");
            }
        }
    }
}
