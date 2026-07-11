using Gamefilled.Application.Notifications;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages.api;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class NotificationUnreadModel : PageModel
{
    private readonly NotificationService _notifications;

    public NotificationUnreadModel(NotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userId = HttpContext.Session.GetInt32("userId");
        if (!userId.HasValue)
            return Unauthorized();

        var preview = await _notifications.GetPreviewAsync(userId.Value, 5, ct);
        var items = preview.Items.Select(item =>
        {
            var isMutual = item.Type == UserNotificationType.MutualConnection;

            return new
            {
                id = item.Id,
                isRead = item.IsRead,
                title = isMutual ? "New mutual connection" : "New follower",
                message = isMutual
                    ? $"You and {item.ActorName} now follow each other."
                    : $"{item.ActorName} started following you.",
                actorName = item.ActorName,
                avatarUrl = item.ActorAvatarUrl,
                createdAt = DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc),
                openUrl = $"/notifications?handler=Open&id={item.Id}"
            };
        });

        return new JsonResult(new
        {
            count = preview.UnreadCount,
            items
        });
    }
}
