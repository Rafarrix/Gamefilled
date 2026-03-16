using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models
{
    [Table("Users")]
    public class User
    {
        public int Id { get; set; }

        public string? Username { get; set; }

        public string? Email { get; set; }

        public string? PasswordHash { get; set; }

        public string? Role { get; set; } = "User";

        public DateTime CreatedAt { get; set; }

        public ICollection<Follow> Followers { get; set; } = new List<Follow>();
        public ICollection<Follow> Following { get; set; } = new List<Follow>();
    }
}