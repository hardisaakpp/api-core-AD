using System;

namespace SCNexus.Core.API.Models
{
    public class AuditLog
    {
        public Guid Id { get; set; }
        public string? UserName { get; set; }
        public string? Action { get; set; }
        public string? TableName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
