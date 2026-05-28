namespace SCNexus.Core.API.DTOs
{
    public class WarehouseAccessInfo
    {
        public string CodAlmacen { get; set; } = string.Empty;
        public string AccesoAlmacen { get; set; } = string.Empty;
        public string? WarehouseGroupId { get; set; }
        public string? WarehouseGroupName { get; set; }
    }

    /// <summary>
    /// Respuesta del endpoint /auth/login
    /// </summary>
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public int ExpiresIn { get; set; } // Segundos hasta expiración
        public UserInfo User { get; set; } = null!;
    }

    /// <summary>
    /// Información del usuario incluida en las respuestas de autenticación
    /// </summary>
    public class UserInfo
    {
        public string Id { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();
        public List<string> Permissions { get; set; } = new();
        public List<WarehouseAccessInfo> WarehouseAccesses { get; set; } = new();
    }

    /// <summary>
    /// Respuesta del endpoint /auth/me
    /// </summary>
    public class MeResponse
    {
        public UserInfo User { get; set; } = null!;
    }

    /// <summary>
    /// Request para validar un token
    /// </summary>
    public class ValidateTokenRequest
    {
        public string Token { get; set; } = string.Empty;
    }

    /// <summary>
    /// Respuesta del endpoint /auth/validate
    /// </summary>
    public class ValidateTokenResponse
    {
        public bool IsValid { get; set; }
        public string? Message { get; set; }
        public UserInfo? User { get; set; }
    }
}
