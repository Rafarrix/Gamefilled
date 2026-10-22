namespace Gamefilled.Identity;

// These types use one canonical GUID user identifier. No legacy int user IDs
// or authenticated-session fallbacks are allowed in the final V2 backend.

public sealed class GamefilledGameEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public GamefilledAccount User { get; set; } = null!;
    public int IgdbGameId { get; set; }
    public string Status { get; set; } = "backlog";
    public int? Rating { get; set; }
    public string? ReviewText { get; set; }
    public bool ContainsSpoilers { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}

public sealed class GamefilledFavoriteGame
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public GamefilledAccount User { get; set; } = null!;
    public int IgdbGameId { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class GamefilledFollow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FollowerId { get; set; }
    public GamefilledAccount Follower { get; set; } = null!;
    public Guid FollowingId { get; set; }
    public GamefilledAccount Following { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class GamefilledActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public GamefilledAccount User { get; set; } = null!;
    public Guid? TargetUserId { get; set; }
    public GamefilledAccount? TargetUser { get; set; }
    public string Type { get; set; } = "";
    public int? IgdbGameId { get; set; }
    public string? MetaJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class GamefilledNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public GamefilledAccount User { get; set; } = null!;
    public Guid? ActorUserId { get; set; }
    public GamefilledAccount? ActorUser { get; set; }
    public string Type { get; set; } = "";
    public string? TargetUrl { get; set; }
    public int? IgdbGameId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}
