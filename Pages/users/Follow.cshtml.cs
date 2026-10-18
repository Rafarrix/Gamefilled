using Gamefilled.Application.Notifications;
using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.users;

public class FollowModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notifications;

    public FollowModel(AppDbContext db, NotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
    }

    public async Task<IActionResult> OnPostAsync(
        string username,
        string? returnUrl = null,
        CancellationToken ct = default)
    {
        var currentUsername = HttpContext.Session.GetString("username");
        if (string.IsNullOrWhiteSpace(currentUsername))
            return RedirectToPage("/users/Login");

        if (string.IsNullOrWhiteSpace(username))
            return NotFound();

        var currentUser = await _db.Users.FirstOrDefaultAsync(
            x => x.Username == currentUsername,
            ct);
        var targetUser = await _db.Users.FirstOrDefaultAsync(
            x => x.Username == username,
            ct);

        if (currentUser == null || targetUser == null)
            return NotFound();

        if (currentUser.Id == targetUser.Id)
            return SafeRedirect(returnUrl, $"/u/{username}");

        var existingFollow = await _db.Follows.FirstOrDefaultAsync(x =>
            x.FollowerId == currentUser.Id &&
            x.FollowingId == targetUser.Id,
            ct);

        if (existingFollow == null)
        {
            _db.Follows.Add(new Follow
            {
                FollowerId = currentUser.Id,
                FollowingId = targetUser.Id,
                CreatedAt = DateTime.UtcNow
            });

            _db.UserActivities.Add(new UserActivity
            {
                UserId = currentUser.Id,
                Type = "followed_user",
                TargetUserId = targetUser.Id,
                CreatedAt = DateTime.UtcNow
            });

            await _notifications.QueueFollowNotificationAsync(currentUser, targetUser, ct);
        }
        else
        {
            _db.Follows.Remove(existingFollow);
        }

        await _db.SaveChangesAsync(ct);
        return SafeRedirect(returnUrl, $"/u/{username}");
    }

    private IActionResult SafeRedirect(string? returnUrl, string fallback)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
            return Redirect(fallback);

        return Redirect(returnUrl);
    }
}
