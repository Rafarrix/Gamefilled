using Gamefilled.Data;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Application.Social;

public sealed record SocialGraphSnapshot(
    IReadOnlySet<int> FollowingIds,
    IReadOnlySet<int> FollowerIds,
    IReadOnlySet<int> MutualIds)
{
    public static SocialGraphSnapshot Empty { get; } = new(
        new HashSet<int>(),
        new HashSet<int>(),
        new HashSet<int>());
}

public sealed class SocialGraphService
{
    private readonly AppDbContext _db;

    public SocialGraphService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<SocialGraphSnapshot> GetSnapshotAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0)
            return SocialGraphSnapshot.Empty;

        var followingIds = (await _db.Follows
                .AsNoTracking()
                .Where(follow => follow.FollowerId == userId)
                .Select(follow => follow.FollowingId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var followerIds = (await _db.Follows
                .AsNoTracking()
                .Where(follow => follow.FollowingId == userId)
                .Select(follow => follow.FollowerId)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var mutualIds = followingIds.Intersect(followerIds).ToHashSet();
        return new SocialGraphSnapshot(followingIds, followerIds, mutualIds);
    }
}
