using System.Security.Claims;
using Gamefilled.Data;
using Gamefilled.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Application.Identity;

/// <summary>
/// Every notification operation is recipient-scoped. Call mutating methods
/// ONLY from antiforgery-protected POST handlers after Identity cutover.
/// </summary>
public sealed class GamefilledV2NotificationService(GamefilledV2DbContext database)
{
    public Task<int> UnreadCountAsync(ClaimsPrincipal viewer, CancellationToken ct = default)
    {
        var owner = GamefilledViewer.RequireUserId(viewer);
        return database.Notifications.AsNoTracking()
            .CountAsync(x => x.UserId == owner && x.ReadAtUtc == null, ct);
    }

    public async Task<GamefilledV2NotificationPreview> GetPreviewAsync(
        ClaimsPrincipal viewer, int limit = 5, CancellationToken ct = default)
    {
        var owner = GamefilledViewer.RequireUserId(viewer);
        limit = Math.Clamp(limit, 1, 8);
        var unread = await UnreadCountAsync(viewer, ct);
        var entries = await database.Notifications.AsNoTracking()
            .Where(n => n.UserId == owner)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(limit)
            .Select(n => new GamefilledV2NotificationPreviewItem(
                n.Id,
                n.Type,
                n.CreatedAtUtc,
                n.ReadAtUtc != null,
                n.ActorUser != null ? n.ActorUser.UserName : null,
                n.ActorUser != null ? n.ActorUser.DisplayName : null,
                n.ActorUser != null ? n.ActorUser.AvatarUrl : null))
            .ToListAsync(ct);

        return new GamefilledV2NotificationPreview(unread, entries);
    }

    public async Task MarkAllReadAsync(ClaimsPrincipal viewer, CancellationToken ct = default)
    {
        var owner = GamefilledViewer.RequireUserId(viewer);
        var unread = await database.Notifications
            .Where(n => n.UserId == owner && n.ReadAtUtc == null)
            .ToListAsync(ct);
        if (unread.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var item in unread)
            item.ReadAtUtc = now;

        await database.SaveChangesAsync(ct);
    }

    public async Task<string?> MarkReadAsync(
        ClaimsPrincipal viewer, Guid notificationId, CancellationToken ct = default)
    {
        var owner = GamefilledViewer.RequireUserId(viewer);
        var notification = await database.Notifications.SingleOrDefaultAsync(
            x => x.Id == notificationId && x.UserId == owner, ct);
        if (notification is null) return null;
        if (notification.ReadAtUtc is null)
        {
            notification.ReadAtUtc = DateTime.UtcNow;
            await database.SaveChangesAsync(ct);
        }

        // Redirect targets are local-only; never redirect to external URLs.
        return IsSafeLocalPath(notification.TargetUrl) ? notification.TargetUrl : null;
    }

    public static bool IsSafeLocalPath(string? target)
    {
        if (string.IsNullOrWhiteSpace(target) || !target.StartsWith('/'))
            return false;
        if (target.StartsWith("//") || target.StartsWith("/\\"))
            return false;
        return !target.Any(char.IsControl);
    }
}

public sealed record GamefilledV2NotificationPreview(
    int UnreadCount,
    IReadOnlyList<GamefilledV2NotificationPreviewItem> Items);

public sealed record GamefilledV2NotificationPreviewItem(
    Guid Id,
    string Type,
    DateTime CreatedAtUtc,
    bool IsRead,
    string? ActorUsername,
    string? ActorDisplayName,
    string? ActorAvatarUrl)
{
    public string ActorName => !string.IsNullOrWhiteSpace(ActorDisplayName)
        ? ActorDisplayName!
        : ActorUsername ?? "A player";
}
