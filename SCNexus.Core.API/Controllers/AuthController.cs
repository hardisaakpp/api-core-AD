using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SCNexus.Core.API.Models;
using SCNexus.Core.API.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SCNexus.Core.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _config;
        private readonly AdLdapSyncService _adLdapSyncService;

        public AuthController(UserManager<AppUser> userManager, IConfiguration config, AdLdapSyncService adLdapSyncService)
        {
            _userManager = userManager;
            _config = config;
            _adLdapSyncService = adLdapSyncService;
        }

        [HttpPost("register")]
        [AllowAnonymous] //CUALQUIER USUARIO PUEDE REGISTRARSE
        public async Task<IActionResult> Register([FromBody] RegisterRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.UserName) || string.IsNullOrWhiteSpace(model.Password))
                return BadRequest("Invalid registration data.");

            var userExists = await _userManager.FindByNameAsync(model.UserName);
            if (userExists != null)
                return BadRequest("User already exists.");

            var user = new AppUser
            {
                UserName = model.UserName,
                Email = model.Email
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { message = "User registered successfully." });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.UserName) || string.IsNullOrWhiteSpace(model.Password))
                return BadRequest("Invalid login data.");

            AppUser? user = null;

            // Autenticación por Active Directory
            if (model.UseActiveDirectory)
            {
                // Verificar primero si el usuario existe localmente y es un usuario AD
                user = await _userManager.FindByNameAsync(model.UserName);
                if (user != null && !string.IsNullOrEmpty(user.ExternalProvider) && user.ExternalProvider != "LDAP")
                {
                    return Unauthorized("This user is not configured for Active Directory authentication.");
                }

                // Intentar autenticación usando PrincipalContext (mismo enfoque que el programa de consola)
                user = await _adLdapSyncService.AuthenticateAdWithPrincipalContextAsync(model.UserName, model.Password, _userManager);

                // Fallback al método LDAP si PrincipalContext no pudo autenticar
                if (user == null)
                {
                    user = await _adLdapSyncService.AuthenticateLdapUserAsync(model.UserName, model.Password, _userManager);
                }

                if (user == null)
                    return Unauthorized("Invalid Active Directory credentials or user not found.");
                
                // Verificar que el usuario autenticado es realmente de AD
                if (string.IsNullOrEmpty(user.ExternalProvider) || user.ExternalProvider != "LDAP")
                {
                    return Unauthorized("User authentication failed: user is not an Active Directory user.");
                }
            }
            // Autenticación local
            else
            {
                user = await _userManager.FindByNameAsync(model.UserName);
                if (user == null)
                    return Unauthorized("Invalid username or password.");
                
                // Verificar que el usuario NO es de AD
                if (!string.IsNullOrEmpty(user.ExternalProvider) && user.ExternalProvider == "LDAP")
                {
                    return Unauthorized("This user must authenticate using Active Directory. Please set useActiveDirectory to true.");
                }
                
                // Verificar contraseña local
                if (!await _userManager.CheckPasswordAsync(user, model.Password))
                    return Unauthorized("Invalid username or password.");
            }

            if (user == null || !user.IsActive)
                return Unauthorized("User account is inactive.");

            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Añadir roles del usuario como claims para que la autorización por Roles funcione
            var userRoles = await _userManager.GetRolesAsync(user);
            foreach (var r in userRoles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, r));
            }

          var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
var jwtIssuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
var jwtAudience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");

var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

var token = new JwtSecurityToken(
    issuer: jwtIssuer,
    audience: jwtAudience,          // <-- aquí usamos Audience correcto
    expires: DateTime.Now.AddHours(3),
    claims: authClaims,
    signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
);


            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                expiration = token.ValidTo
            });
        }
    }
}
