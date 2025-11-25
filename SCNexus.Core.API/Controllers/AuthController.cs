using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SCNexus.Core.API.DTOs;
using SCNexus.Core.API.Models;
using SCNexus.Core.API.Services;

namespace SCNexus.Core.API.Controllers
{
    /// <summary>
    /// CORE / Identity API - Solo autentica y emite JWT
    /// Los módulos externos solo reciben el token y validan roles/permisos
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AuthService _authService;
        private readonly AdLdapSyncService _adLdapSyncService;

        public AuthController(
            UserManager<AppUser> userManager,
            AuthService authService,
            AdLdapSyncService adLdapSyncService)
        {
            _userManager = userManager;
            _authService = authService;
            _adLdapSyncService = adLdapSyncService;
        }

        /// <summary>
        /// (1) POST /auth/login
        /// Retorna JWT con roles y permisos del usuario
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.UserName) || string.IsNullOrWhiteSpace(model.Password))
                return BadRequest(new { message = "Invalid login data." });

            AppUser? user = null;

            // Autenticación por Active Directory
            if (model.UseActiveDirectory)
            {
                user = await _userManager.FindByNameAsync(model.UserName);
                if (user != null && !string.IsNullOrEmpty(user.ExternalProvider) && user.ExternalProvider != "LDAP")
                {
                    return Unauthorized(new { message = "This user is not configured for Active Directory authentication." });
                }

                user = await _adLdapSyncService.AuthenticateAdWithPrincipalContextAsync(model.UserName, model.Password, _userManager);

                if (user == null)
                {
                    user = await _adLdapSyncService.AuthenticateLdapUserAsync(model.UserName, model.Password, _userManager);
                }

                if (user == null)
                    return Unauthorized(new { message = "Invalid Active Directory credentials or user not found." });
                
                if (string.IsNullOrEmpty(user.ExternalProvider) || user.ExternalProvider != "LDAP")
                {
                    return Unauthorized(new { message = "User authentication failed: user is not an Active Directory user." });
                }
            }
            // Autenticación local
            else
            {
                user = await _userManager.FindByNameAsync(model.UserName);
                if (user == null)
                    return Unauthorized(new { message = "Invalid username or password." });
                
                if (!string.IsNullOrEmpty(user.ExternalProvider) && user.ExternalProvider == "LDAP")
                {
                    return Unauthorized(new { message = "This user must authenticate using Active Directory. Please set useActiveDirectory to true." });
                }
                
                if (!await _userManager.CheckPasswordAsync(user, model.Password))
                    return Unauthorized(new { message = "Invalid username or password." });
            }

            if (user == null || !user.IsActive)
                return Unauthorized(new { message = "User account is inactive." });

            // Generar token con roles y permisos
            var response = await _authService.GenerateTokenAsync(user);
            return Ok(response);
        }

        /// <summary>
        /// (2) GET /auth/me
        /// Retorna información del usuario autenticado
        /// Los módulos pueden usar esto para obtener datos del usuario actual
        /// </summary>
        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            var userInfo = await _authService.GetCurrentUserInfoAsync(User);
            if (userInfo == null)
                return Unauthorized(new { message = "User not found." });

            return Ok(new MeResponse { User = userInfo });
        }

        /// <summary>
        /// (3) GET /auth/validate
        /// Valida si un token es válido y retorna información del usuario
        /// Los módulos pueden usar esto para verificar tokens
        /// </summary>
        [HttpPost("validate")]
        [AllowAnonymous]
        public async Task<IActionResult> Validate([FromBody] ValidateTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Token))
                return BadRequest(new { message = "Token is required." });

            var result = await _authService.ValidateTokenAsync(request.Token);
            
            if (!result.IsValid)
                return Ok(result); // Retornar 200 con IsValid=false para que el módulo maneje el estado

            return Ok(result);
        }
    }
}

