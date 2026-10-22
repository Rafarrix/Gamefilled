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
