using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCNexus.Core.API.Models
{
    [Table("asp_net_user_warehouse_access")]
    public class AspNetUserWarehouseAccess
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string CodAlmacen { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string AccesoAlmacen { get; set; } = string.Empty;

        public Guid? WarehouseGroupId { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public DateTime? FechaModificacion { get; set; }

        [MaxLength(256)]
        public string? ModificadoPor { get; set; }

        // Navigation properties
        public AppUser User { get; set; } = null!;
        public WarehouseGroup? WarehouseGroup { get; set; }
    }
}
