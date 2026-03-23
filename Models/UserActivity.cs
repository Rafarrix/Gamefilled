using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models
{
    [Table("UserActivities")]
    public class UserActivity
    {
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public string Type { get; set; } = "";

        public int? TargetUserId { get; set; }
        public int? GameId { get; set; }
        public string? MetaJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [ForeignKey(nameof(TargetUserId))]
        public User? TargetUser { get; set; }
    }
}