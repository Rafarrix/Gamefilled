using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Gamefilled.Models;

[Table("UserGameEntries")]
public sealed class UserGameEntry
{
    public int Id { get; set; }

    [Required]
    public int UserId { get; set; }

    [Required]
    public int GameId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = GameLibraryStatus.Backlog;

    [Range(1, 10)]
    public int? Rating { get; set; }

    [MaxLength(5000)]
    public string? ReviewText { get; set; }

    public bool ContainsSpoilers { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }
}

public static class GameLibraryStatus
{
    public const string Played = "played";
    public const string Playing = "playing";
    public const string Backlog = "backlog";
    public const string Wishlist = "wishlist";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(
        [Played, Playing, Backlog, Wishlist],
        StringComparer.OrdinalIgnoreCase);

    public static string? Normalize(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized is not null && All.Contains(normalized)
            ? normalized
            : null;
    }
}
