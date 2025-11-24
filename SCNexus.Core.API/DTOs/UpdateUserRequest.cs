namespace SCNexus.Core.API.DTOs
{
    public class UpdateUserRequest
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public bool? IsActive { get; set; }
    }
}
