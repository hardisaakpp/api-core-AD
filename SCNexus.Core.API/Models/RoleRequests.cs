namespace SCNexus.Core.API.Models
{
    public class CreateRoleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    public class UpdateRoleRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>Rol con lo necesario para listarlo y gestionarlo (el IdentityRole crudo no trae descripción ni conteos).</summary>
    public class RoleResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? NormalizedName { get; set; }
        public string Description { get; set; } = string.Empty;
        public int UsersCount { get; set; }
        public int PermissionsCount { get; set; }
        /// <summary>Rol del sistema: el Core lo recrea al arrancar o el código lo referencia por nombre. No se renombra ni se elimina.</summary>
        public bool IsSystem { get; set; }
    }
}
