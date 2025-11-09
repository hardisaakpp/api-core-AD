using System;
using System.ComponentModel.DataAnnotations;

namespace SCNexus.Core.API.Models
{
    public class AuditLog
    {
        [Key]
        public Guid Id { get; set; }

        [MaxLength(100)]
        public string? UserName { get; set; }

        [MaxLength(50)]
        public string? Action { get; set; }

        [MaxLength(100)]
        public string? TableName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
