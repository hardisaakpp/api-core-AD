using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SCNexus.Core.API.Services;

namespace SCNexus.Core.API.Controllers
{
    /// <summary>Respuesta de POST /api/Admin/sync-ldap (202 iniciada, 409 ya en curso).</summary>
    public sealed record LdapSyncStartResponse(string Message, LdapSyncStatus Status);

    /// <summary>
    /// Sincronización masiva de usuarios con Active Directory. Conserva la ruta histórica
    /// /api/Admin/sync-ldap que consume el front; separada de <see cref="AdminController"/> para
    /// que cada controlador tenga una sola responsabilidad.
    /// </summary>
    [ApiController]
    [Route("api/Admin")]
    [Authorize(Roles = "Admin")]
    public class LdapSyncController : ControllerBase
    {
        private readonly LdapSyncRunner _syncRunner;

        public LdapSyncController(LdapSyncRunner syncRunner)
        {
            _syncRunner = syncRunner;
        }

        /// <summary>
        /// Starts a full AD synchronisation in the background and returns 202 with the current
        /// status; poll GET sync-ldap/status for the result. Running it inline used to time out at
        /// the gateway on large directories. 409 when a run is already in progress.
        /// </summary>
        [HttpPost("sync-ldap")]
        [ProducesResponseType(typeof(LdapSyncStartResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(typeof(LdapSyncStartResponse), StatusCodes.Status409Conflict)]
        public IActionResult SyncLdap()
        {
            var trigger = $"manual:{User.Identity?.Name ?? "admin"}";
            if (!_syncRunner.TryStartInBackground(trigger))
            {
                return Conflict(new LdapSyncStartResponse("Ya hay una sincronización de AD en curso.", _syncRunner.Status));
            }

            return Accepted(new LdapSyncStartResponse("Sincronización de AD iniciada.", _syncRunner.Status));
        }

        /// <summary>Last result, whether a run is in progress and when the nightly job fires next.</summary>
        [HttpGet("sync-ldap/status")]
        [ProducesResponseType(typeof(LdapSyncStatus), StatusCodes.Status200OK)]
        public IActionResult SyncLdapStatus() => Ok(_syncRunner.Status);
    }
}
