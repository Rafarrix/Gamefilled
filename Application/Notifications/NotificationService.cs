using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Application.Notifications;

public sealed class NotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default) =>
        _db.UserNotifications
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && x.ReadAt == null, ct);

    public async Task<NotificationPreview> GetPreviewAsync(
        int userId,
        int limit = 5,
        CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 8);

        var unreadCount = await GetUnreadCountAsync(userId, ct);
        var items = await _db.UserNotifications
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(limit)
            .Select(x => new NotificationPreviewItem(
                x.Id,
                x.Type,
                x.CreatedAt,
                x.ReadAt != null,
                x.ActorUser != null ? x.ActorUser.Username : null,
                x.ActorUser != null ? x.ActorUser.DisplayName : null,
                x.ActorUser != null ? x.ActorUser.AvatarUrl : null))
            .ToListAsync(ct);

        return new NotificationPreview(unreadCount, items);
    }

    public async Task QueueFollowNotificationAsync(
        User actor,
        User recipient,
        CancellationToken ct = default)
    {
        if (actor.Id == recipient.Id)
            return;

        var reverseFollowExists = await _db.Follows
            .AsNoTracking()
            .AnyAsync(x =>
                x.FollowerId == recipient.Id &&
                x.FollowingId == actor.Id,
                ct);

        var type = reverseFollowExists
            ? UserNotificationType.MutualConnection
            : UserNotificationType.NewFollower;

        var duplicateWindow = DateTime.UtcNow.AddMinutes(-10);
        var duplicateExists = await _db.UserNotifications
            .AsNoTracking()
            .AnyAsync(x =>
                x.UserId == recipient.Id &&
                x.ActorUserId == actor.Id &&
                x.Type == type &&
                x.CreatedAt >= duplicateWindow,
                ct);

        if (duplicateExists)
            return;

        _db.UserNotifications.Add(new UserNotification
        {
            UserId = recipient.Id,
            ActorUserId = actor.Id,
            Type = type,
            TargetUrl = $"/u/{Uri.EscapeDataString(actor.Username ?? actor.Id.ToString())}",
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<string?> MarkReadAndGetTargetAsync(
        int userId,
        int notificationId,
        CancellationToken ct = default)
    {
        var notification = await _db.UserNotifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, ct);

        if (notification == null)
            return null;

        if (!notification.ReadAt.HasValue)
        {
            notification.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        return notification.TargetUrl;
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken ct = default)
    {
        var unread = await _db.UserNotifications
            .Where(x => x.UserId == userId && x.ReadAt == null)
            .ToListAsync(ct);

        if (unread.Count == 0)
            return;

        var readAt = DateTime.UtcNow;
        foreach (var notification in unread)
            notification.ReadAt = readAt;

        await _db.SaveChangesAsync(ct);
    }
}

public sealed record NotificationPreview(
    int UnreadCount,
    IReadOnlyList<NotificationPreviewItem> Items);

public sealed record NotificationPreviewItem(
    int Id,
    string Type,
    DateTime CreatedAt,
    bool IsRead,
    string? ActorUsername,
    string? ActorDisplayName,
    string? ActorAvatarUrl)
{
    public string ActorName =>
        !string.IsNullOrWhiteSpace(ActorDisplayName)
            ? ActorDisplayName!
            : ActorUsername ?? "A player";
}
