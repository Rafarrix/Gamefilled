using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models;

[Table("UserNotifications")]
public sealed class UserNotification
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    public int? ActorUserId { get; set; }

    [Required, MaxLength(64)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? TargetUrl { get; set; }

    public int? GameId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [ForeignKey(nameof(ActorUserId))]
    public User? ActorUser { get; set; }
}

public static class UserNotificationType
{
    public const string NewFollower = "new_follower";
    public const string MutualConnection = "mutual_connection";
}
