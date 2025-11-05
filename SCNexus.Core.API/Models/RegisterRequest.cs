namespace SCNexus.Core.API.Models
{
    public class RegisterRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string Password { get; set; } = string.Empty;
    }
}
