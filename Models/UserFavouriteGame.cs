using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models
{
    [Table("UserFavoriteGames")]
    public class UserFavoriteGame
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int GameId { get; set; }

        [Required]
        public int SortOrder { get; set; }

        public bool IsPrimary { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }
    }
}