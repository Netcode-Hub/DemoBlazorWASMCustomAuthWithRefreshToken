namespace DemoBlazorWASMCustomAuthWithRefreshToken.Server.AuthenticationModel
{
    public class UserRole
    {
        public int Id { get; set; }
        public string? RoleName { get; set; }
        public int UserId { get; set; }
    }
}
