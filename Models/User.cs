namespace Gamefilled.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Role { get; set; } = "User";

        public string Login { get; set; } = string.Empty;

        public string Username{ get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public string? Email { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
