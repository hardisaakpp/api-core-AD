namespace SCNexus.Core.API.DTOs
{
    public class ChangePasswordRequest
    {
        public string? OldPassword { get; set; }
        public string? NewPassword { get; set; }
    }
}
