using Microsoft.AspNetCore.Identity;

namespace SCNexus.Core.API.Models
{
    public class AppUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // External identity provider (e.g. "LDAP", "AzureAD")
        public string? ExternalProvider { get; set; }
        // External id from provider (e.g. sAMAccountName, oid)
        public string? ExternalId { get; set; }
    }
}
