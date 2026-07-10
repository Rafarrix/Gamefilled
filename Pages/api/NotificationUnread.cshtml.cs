using Gamefilled.Application.Notifications;
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

        var count = await _notifications.GetUnreadCountAsync(userId.Value, ct);
        return new JsonResult(new { count });
    }
}
