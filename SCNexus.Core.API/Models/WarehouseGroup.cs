using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SCNexus.Core.API.Models
{
    [Table("asp_net_warehouse_group")]
    public class WarehouseGroup
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Grupo { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? Descripcion { get; set; }

        public List<string> Almacenes { get; set; } = [];

        [MaxLength(50)]
        public string? BodegaCentral { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public DateTime? FechaModificacion { get; set; }

        [MaxLength(256)]
        public string? ModificadoPor { get; set; }
    }
}
