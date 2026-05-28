using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using SCNexus.Core.API.Data;
using SCNexus.Core.API.DTOs;
using SCNexus.Core.API.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace SCNexus.Core.API.Services
{
    /// <summary>
    /// Servicio de autenticación centralizado para el Core Identity API
    /// </summary>
    public class AuthService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AuthService(
            UserManager<AppUser> userManager,
            AppDbContext context,
            IConfiguration config)
        {
            _userManager = userManager;
            _context = context;
            _config = config;
        }

        private static readonly JsonSerializerOptions _camelCaseOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        /// <summary>
        /// Genera un JWT con roles, permisos y accesos a almacenes del usuario
        /// </summary>
        public async Task<LoginResponse> GenerateTokenAsync(AppUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var permissions = await GetUserPermissionsAsync(user.Id);
            var warehouseAccesses = await GetUserWarehouseAccessesAsync(user.Id);

            var authClaims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            // Añadir roles como claims
            foreach (var role in roles)
            {
                authClaims.Add(new Claim(ClaimTypes.Role, role));
            }

            // Añadir permisos como claims personalizados
            foreach (var permission in permissions)
            {
                authClaims.Add(new Claim("permission", permission));
            }

            // Añadir accesos a almacenes como claims JSON
            foreach (var wa in warehouseAccesses)
            {
                authClaims.Add(new Claim("warehouse_access", JsonSerializer.Serialize(wa, _camelCaseOptions)));
            }

            var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
            var jwtIssuer = _config["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
            var jwtAudience = _config["Jwt:Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");

            var authSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

            var expiresAt = DateTime.UtcNow.AddHours(3);
            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                expires: expiresAt,
                claims: authClaims,
                signingCredentials: new SigningCredentials(authSigningKey, SecurityAlgorithms.HmacSha256)
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            var expiresIn = (int)(expiresAt - DateTime.UtcNow).TotalSeconds;

            return new LoginResponse
            {
                Token = tokenString,
                ExpiresIn = expiresIn,
                User = new UserInfo
                {
                    Id = user.Id,
                    UserName = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    FullName = user.FullName,
                    Roles = roles.ToList(),
                    Permissions = permissions,
                    WarehouseAccesses = warehouseAccesses
                }
            };
        }

        /// <summary>
        /// Obtiene los accesos a almacenes asignados al usuario
        /// </summary>
        public async Task<List<WarehouseAccessInfo>> GetUserWarehouseAccessesAsync(string userId)
        {
            return await _context.UserWarehouseAccesses
                .Where(w => w.UserId == userId)
                .Include(w => w.WarehouseGroup)
                .Select(w => new WarehouseAccessInfo
                {
                    CodAlmacen = w.CodAlmacen,
                    AccesoAlmacen = w.AccesoAlmacen,
                    WarehouseGroupId = w.WarehouseGroupId.HasValue ? w.WarehouseGroupId.Value.ToString() : null,
                    WarehouseGroupName = w.WarehouseGroup != null ? w.WarehouseGroup.Grupo : null
                })
                .ToListAsync();
        }

        /// <summary>
        /// Obtiene todos los permisos de un usuario basado en sus roles
        /// </summary>
        public async Task<List<string>> GetUserPermissionsAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return new List<string>();

            var roles = await _userManager.GetRolesAsync(user);
            
            var permissions = await _context.RolePermissions
                .Include(rp => rp.Role)
                .Include(rp => rp.Permission)
                .Where(rp => roles.Contains(rp.Role.Name!))
                .Select(rp => rp.Permission.Key)
                .Distinct()
                .ToListAsync();

            return permissions;
        }

        /// <summary>
        /// Valida un token JWT y retorna la información del usuario
        /// </summary>
        public async Task<ValidateTokenResponse> ValidateTokenAsync(string token)
        {
            try
            {
                var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
                var jwtIssuer = _config["Jwt:Issuer"];
                var jwtAudience = _config["Jwt:Audience"];

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(jwtKey);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
                
                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return new ValidateTokenResponse { IsValid = false, Message = "Invalid token: missing user ID" };
                }

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null || !user.IsActive)
                {
                    return new ValidateTokenResponse { IsValid = false, Message = "User not found or inactive" };
                }

                var roles = await _userManager.GetRolesAsync(user);
                var permissions = await GetUserPermissionsAsync(user.Id);
                var warehouseAccesses = await GetUserWarehouseAccessesAsync(user.Id);

                return new ValidateTokenResponse
                {
                    IsValid = true,
                    Message = "Token is valid",
                    User = new UserInfo
                    {
                        Id = user.Id,
                        UserName = user.UserName ?? string.Empty,
                        Email = user.Email ?? string.Empty,
                        FullName = user.FullName,
                        Roles = roles.ToList(),
                        Permissions = permissions,
                        WarehouseAccesses = warehouseAccesses
                    }
                };
            }
            catch (SecurityTokenExpiredException)
            {
                return new ValidateTokenResponse { IsValid = false, Message = "Token has expired" };
            }
            catch (Exception ex)
            {
                return new ValidateTokenResponse { IsValid = false, Message = $"Token validation failed: {ex.Message}" };
            }
        }

        /// <summary>
        /// Obtiene información del usuario actual desde el token
        /// </summary>
        public async Task<UserInfo?> GetCurrentUserInfoAsync(ClaimsPrincipal principal)
        {
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return null;

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            var permissions = await GetUserPermissionsAsync(user.Id);
            var warehouseAccesses = await GetUserWarehouseAccessesAsync(user.Id);

            return new UserInfo
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                Roles = roles.ToList(),
                Permissions = permissions,
                WarehouseAccesses = warehouseAccesses
            };
        }
    }
}
