namespace SCNexus.Core.API.Models
{
    /// <summary>
    /// Representa un permiso específico en el sistema (ej: "depositos.view", "depositos.create")
    /// </summary>
    public class Permission
    {
        public int Id { get; set; }
        
        /// <summary>
        /// Clave única del permiso (ej: "depositos.view", "users.create")
        /// </summary>
        public string Key { get; set; } = string.Empty;
        
        /// <summary>
        /// Descripción legible del permiso
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Módulo al que pertenece (ej: "depositos", "contabilidad", "usuarios")
        /// </summary>
        public string Module { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Relación muchos a muchos con Roles
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
