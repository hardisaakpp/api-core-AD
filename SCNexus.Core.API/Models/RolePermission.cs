namespace SCNexus.Core.API.Models
{
    /// <summary>
    /// Tabla de unión entre Roles y Permissions
    /// </summary>
    public class RolePermission
    {
        public string RoleId { get; set; } = string.Empty;
        public int PermissionId { get; set; }
        
        // Navigation properties
        public Microsoft.AspNetCore.Identity.IdentityRole Role { get; set; } = null!;
        public Permission Permission { get; set; } = null!;
    }
}
