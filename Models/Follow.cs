using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models
{
    [Table("Follows")]
    public class Follow
    {
        public int Id { get; set; }

        [Required]
        public int FollowerId { get; set; }

        [Required]
        public int FollowingId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(FollowerId))]
        public User? Follower { get; set; }

        [ForeignKey(nameof(FollowingId))]
        public User? Following { get; set; }
    }
}