using System.Security.Claims;
using Gamefilled.Data;
using Gamefilled.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Application.Identity;

/// <summary>
/// V2 GUID relationship service. Caller identity comes from a verified
/// principal, never from a client form field or legacy session.
/// </summary>
public sealed class GamefilledV2SocialService(GamefilledV2DbContext database)
{
    public async Task<bool> FollowAsync(
        ClaimsPrincipal viewer, Guid targetId, CancellationToken ct = default)
    {
        var actor = GamefilledViewer.RequireUserId(viewer);
        if (targetId == Guid.Empty || targetId == actor) return false;

        // Both directions of blocking forbid follows. Private profiles
        // require a separate consent/follow-request flow not yet implemented.
        var restricted = await database.Blocks.AsNoTracking().AnyAsync(
            x => (x.BlockerId == actor && x.BlockedId == targetId)
              || (x.BlockerId == targetId && x.BlockedId == actor), ct);
        if (restricted) return false;

        var target = await database.Users.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == targetId, ct);
        if (target is null || target.IsProfilePrivate) return false;

        var exists = await database.Follows.AnyAsync(
            x => x.FollowerId == actor && x.FollowingId == targetId, ct);
        if (exists) return false;

        database.Follows.Add(new GamefilledFollow
        {
            FollowerId = actor,
            FollowingId = targetId
        });
        await database.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UnfollowAsync(
        ClaimsPrincipal viewer, Guid targetId, CancellationToken ct = default)
    {
        var actor = GamefilledViewer.RequireUserId(viewer);
        var row = await database.Follows.SingleOrDefaultAsync(
            x => x.FollowerId == actor && x.FollowingId == targetId, ct);
        if (row is null) return false;

        database.Follows.Remove(row);
        await database.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> BlockAsync(
        ClaimsPrincipal viewer, Guid targetId, CancellationToken ct = default)
    {
        var actor = GamefilledViewer.RequireUserId(viewer);
        if (targetId == Guid.Empty || targetId == actor) return false;

        var targetExists = await database.Users.AsNoTracking()
            .AnyAsync(x => x.Id == targetId, ct);
        if (!targetExists) return false;

        var exists = await database.Blocks.AnyAsync(
            x => x.BlockerId == actor && x.BlockedId == targetId, ct);
        if (exists) return false;

        database.Blocks.Add(new GamefilledBlock
        {
            BlockerId = actor,
            BlockedId = targetId
        });

        // A block dissolves both existing follow directions atomically
        // with the new block in the same SaveChanges transaction.
        var follows = await database.Follows.Where(x =>
            (x.FollowerId == actor && x.FollowingId == targetId)
            || (x.FollowerId == targetId && x.FollowingId == actor))
            .ToListAsync(ct);
        database.Follows.RemoveRange(follows);

        await database.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UnblockAsync(
        ClaimsPrincipal viewer, Guid targetId, CancellationToken ct = default)
    {
        var actor = GamefilledViewer.RequireUserId(viewer);
        var block = await database.Blocks.SingleOrDefaultAsync(
            x => x.BlockerId == actor && x.BlockedId == targetId, ct);
        if (block is null) return false;

        database.Blocks.Remove(block);
        await database.SaveChangesAsync(ct);
        return true;
    }
}
