namespace SCNexus.Core.API.Models
{
    /// <summary>
    /// Descripción legible de un rol. ASP.NET Identity no la guarda (IdentityRole solo tiene nombre), así que vive en
    /// una tabla aparte, 1:1 con AspNetRoles. Un rol sin fila aquí simplemente no tiene descripción.
    /// </summary>
    public class RoleDescription
    {
        public const int MaxLength = 500;

        public string RoleId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
