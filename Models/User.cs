using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models
{
    // Garante que o EF vai mapear para a tabela dbo.Users
    [Table("Users")]
    public class User
    {
        public int Id { get; set; }

        // Username pode ser null em registos antigos (até limpares a BD)
        public string? Username { get; set; }

        // Email pode ser null em registos antigos
        public string? Email { get; set; }

        // ✅ NOVO: password segura (hash + salt)
        public string? PasswordHash { get; set; }

        // Role pode vir null em dados antigos, mas o sistema assume "User" por defeito
        public string? Role { get; set; } = "User";

        public DateTime CreatedAt { get; set; }
    }
}
