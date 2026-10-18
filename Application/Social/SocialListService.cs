using Gamefilled.Data;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Application.Social;

public sealed record SocialListUserCard(
    int Id,
    string Username,
    string DisplayName,
    string? Bio,
    string? AvatarUrl,
    bool IsOnline,
    string PresenceText,
    bool IsMutual);

public sealed class SocialListService
{
    private readonly AppDbContext _db;
    private readonly SocialGraphService _socialGraph;

    public SocialListService(AppDbContext db, SocialGraphService socialGraph)
    {
        _db = db;
        _socialGraph = socialGraph;
    }

    public async Task<IReadOnlyList<SocialListUserCard>> GetFollowersAsync(
        int profileUserId,
        CancellationToken cancellationToken = default)
    {
        var graph = await _socialGraph.GetSnapshotAsync(profileUserId, cancellationToken);
        var utcNow = DateTime.UtcNow;

        var rows = await _db.Follows
            .AsNoTracking()
            .Where(follow => follow.FollowingId == profileUserId)
            .OrderByDescending(follow => follow.CreatedAt)
            .Select(follow => new SocialListRow(
                follow.Follower!.Id,
                follow.Follower.Username!,
                follow.Follower.DisplayName,
                follow.Follower.Bio,
                follow.Follower.AvatarUrl,
                follow.Follower.LastSeenAt))
            .ToListAsync(cancellationToken);

        return BuildCards(rows, graph.MutualIds, utcNow);
    }

    public async Task<IReadOnlyList<SocialListUserCard>> GetFollowingAsync(
        int profileUserId,
        CancellationToken cancellationToken = default)
    {
        var graph = await _socialGraph.GetSnapshotAsync(profileUserId, cancellationToken);
        var utcNow = DateTime.UtcNow;

        var rows = await _db.Follows
            .AsNoTracking()
            .Where(follow => follow.FollowerId == profileUserId)
            .OrderByDescending(follow => follow.CreatedAt)
            .Select(follow => new SocialListRow(
                follow.Following!.Id,
                follow.Following.Username!,
                follow.Following.DisplayName,
                follow.Following.Bio,
                follow.Following.AvatarUrl,
                follow.Following.LastSeenAt))
            .ToListAsync(cancellationToken);

        return BuildCards(rows, graph.MutualIds, utcNow);
    }

    private static IReadOnlyList<SocialListUserCard> BuildCards(
        IEnumerable<SocialListRow> rows,
        IReadOnlySet<int> mutualIds,
        DateTime utcNow) => rows
        .Select(row => new SocialListUserCard(
            row.Id,
            row.Username,
            string.IsNullOrWhiteSpace(row.DisplayName) ? row.Username : row.DisplayName,
            row.Bio,
            row.AvatarUrl,
            UserPresence.IsOnline(row.LastSeenAt, utcNow),
            UserPresence.Describe(row.LastSeenAt, utcNow),
            mutualIds.Contains(row.Id)))
        .OrderByDescending(card => card.IsOnline)
        .ThenBy(card => card.DisplayName)
        .ToList();

    private sealed record SocialListRow(
        int Id,
        string Username,
        string? DisplayName,
        string? Bio,
        string? AvatarUrl,
        DateTime? LastSeenAt);
}
