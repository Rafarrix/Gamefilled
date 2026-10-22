using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace Gamefilled.Identity;

/// <summary>
/// Canonical account for the integrated V2. This replaces legacy integer/session
/// authority at the coordinated cutover; it is never a second live login.
/// </summary>
public sealed class GamefilledAccount : IdentityUser<Guid>
{
    [MaxLength(100)]
    public string? DisplayName { get; set; }

    [MaxLength(500)]
    public string? Bio { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [MaxLength(500)]
    public string? BannerUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAtUtc { get; set; }
    public bool IsProfilePrivate { get; set; }

    public ICollection<GamefilledGameEntry> GameEntries { get; set; } = [];
    public ICollection<GamefilledFavoriteGame> FavoriteGames { get; set; } = [];
}
