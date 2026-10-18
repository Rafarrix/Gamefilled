using Gamefilled.Application.Notifications;
using Gamefilled.Data;
using Gamefilled.Infrastructure;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages;

public sealed class NotificationsModel : ProtectedPageModel
{
    private const int PageSize = 20;

    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;

    public NotificationsModel(AppDbContext db, NotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    [BindProperty(SupportsGet = true, Name = "filter")]
    public string Filter { get; set; } = "all";

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public List<NotificationItem> Items { get; private set; } = new();
    public int TotalCount { get; private set; }
    public int UnreadCount { get; private set; }
    public int TotalPages { get; private set; } = 1;

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var loginResult = RequireLogin();
        if (loginResult != null)
            return loginResult;

        var userId = CurrentUserId!.Value;
        Filter = Filter.Equals("unread", StringComparison.OrdinalIgnoreCase)
            ? "unread"
            : "all";
        PageNumber = Math.Max(1, PageNumber);

        UnreadCount = await _notifications.GetUnreadCountAsync(userId, ct);
        TotalCount = await _db.UserNotifications
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId, ct);

        var query = _db.UserNotifications
            .AsNoTracking()
            .Where(x => x.UserId == userId);

        if (Filter == "unread")
            query = query.Where(x => x.ReadAt == null);

        var filteredCount = await query.CountAsync(ct);
        TotalPages = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)PageSize));
        PageNumber = Math.Min(PageNumber, TotalPages);

        Items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .Select(x => new NotificationItem
            {
                Id = x.Id,
                Type = x.Type,
                TargetUrl = x.TargetUrl,
                CreatedAt = x.CreatedAt,
                IsRead = x.ReadAt != null,
                ActorUsername = x.ActorUser != null ? x.ActorUser.Username : null,
                ActorDisplayName = x.ActorUser != null ? x.ActorUser.DisplayName : null,
                ActorAvatarUrl = x.ActorUser != null ? x.ActorUser.AvatarUrl : null
            })
            .ToListAsync(ct);

        return Page();
    }

    public async Task<IActionResult> OnGetOpenAsync(int id, CancellationToken ct)
    {
        var loginResult = RequireLogin();
        if (loginResult != null)
            return loginResult;

        var target = await _notifications.MarkReadAndGetTargetAsync(
            CurrentUserId!.Value,
            id,
            ct);

        return !string.IsNullOrWhiteSpace(target) && Url.IsLocalUrl(target)
            ? LocalRedirect(target)
            : RedirectToPage("/Notifications");
    }

    public async Task<IActionResult> OnPostOpenAsync(int id, CancellationToken ct)
    {
        var loginResult = RequireLogin();
        if (loginResult != null)
            return loginResult;

        var target = await _notifications.MarkReadAndGetTargetAsync(
            CurrentUserId!.Value,
            id,
            ct);

        return !string.IsNullOrWhiteSpace(target) && Url.IsLocalUrl(target)
            ? LocalRedirect(target)
            : RedirectToPage("/Notifications");
    }

    public async Task<IActionResult> OnPostMarkAllReadAsync(
        string? filter,
        int page = 1,
        CancellationToken ct = default)
    {
        var loginResult = RequireLogin();
        if (loginResult != null)
            return loginResult;

        await _notifications.MarkAllReadAsync(CurrentUserId!.Value, ct);
        return RedirectToPage("/Notifications", new
        {
            filter = filter == "unread" ? "unread" : "all",
            page = Math.Max(1, page)
        });
    }

    public string PageUrl(int page) =>
        $"/notifications?filter={Uri.EscapeDataString(Filter)}&page={Math.Max(1, page)}";

    public static string RelativeTime(DateTime utc)
    {
        var value = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        var elapsed = DateTime.UtcNow - value;

        if (elapsed.TotalMinutes < 1) return "Just now";
        if (elapsed.TotalMinutes < 60) return $"{Math.Max(1, (int)elapsed.TotalMinutes)}m ago";
        if (elapsed.TotalHours < 24) return $"{Math.Max(1, (int)elapsed.TotalHours)}h ago";
        if (elapsed.TotalDays < 7) return $"{Math.Max(1, (int)elapsed.TotalDays)}d ago";
        return value.ToLocalTime().ToString("MMM d, yyyy");
    }

    public sealed class NotificationItem
    {
        public int Id { get; init; }
        public string Type { get; init; } = string.Empty;
        public string? TargetUrl { get; init; }
        public DateTime CreatedAt { get; init; }
        public bool IsRead { get; init; }
        public string? ActorUsername { get; init; }
        public string? ActorDisplayName { get; init; }
        public string? ActorAvatarUrl { get; init; }

        public string ActorName =>
            !string.IsNullOrWhiteSpace(ActorDisplayName)
                ? ActorDisplayName!
                : ActorUsername ?? "A player";

        public string Title => Type == UserNotificationType.MutualConnection
            ? "New mutual connection"
            : "New follower";

        public string Message => Type == UserNotificationType.MutualConnection
            ? $"You and {ActorName} now follow each other."
            : $"{ActorName} started following you.";

        public string Icon => Type == UserNotificationType.MutualConnection
            ? "fas fa-user-group"
            : "fas fa-user-plus";
    }
}
