using Gamefilled.V2.Identity;

namespace Gamefilled.V2.Library;

/// <summary>
/// Gamefilled's manually curated game collection. Steam ownership and playtime
/// are independent metadata, not an automatic status or completed indicator.
/// </summary>
public sealed class GameLibraryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public GamefilledUser User { get; set; } = null!;
    public int IgdbGameId { get; set; }
    public string Status { get; set; } = GameLibraryStatus.Backlog;
    public int? Rating { get; set; }
    public string? ReviewText { get; set; }
    public bool ContainsSpoilers { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}

public static class GameLibraryStatus
{
    public const string Played = "played";
    public const string Playing = "playing";
    public const string Backlog = "backlog";
    public const string Wishlist = "wishlist";

    public static readonly IReadOnlySet<string> Valid = new HashSet<string>(
        [Played, Playing, Backlog, Wishlist], StringComparer.Ordinal);

    public static string? Normalize(string? value)
    {
        var candidate = value?.Trim().ToLowerInvariant();
        return candidate is not null && Valid.Contains(candidate) ? candidate : null;
    }
}
