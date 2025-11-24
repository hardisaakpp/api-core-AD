namespace SCNexus.Core.API.Models
{
    public class LoginRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseActiveDirectory { get; set; } = false;
    }
}
