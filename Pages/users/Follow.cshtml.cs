using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.users
{
    public class FollowModel : PageModel
    {
        private readonly AppDbContext _db;

        public FollowModel(AppDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> OnPostAsync(string username, string? returnUrl = null)
        {
            var currentUsername = HttpContext.Session.GetString("username");
            if (string.IsNullOrWhiteSpace(currentUsername))
                return RedirectToPage("/users/Login");

            if (string.IsNullOrWhiteSpace(username))
                return NotFound();

            var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == currentUsername);
            var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (currentUser == null || targetUser == null)
                return NotFound();

            if (currentUser.Id == targetUser.Id)
                return Redirect(returnUrl ?? $"/u/{username}");

            var existingFollow = await _db.Follows.FirstOrDefaultAsync(f =>
                f.FollowerId == currentUser.Id &&
                f.FollowingId == targetUser.Id);

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
            }
            else
            {
                _db.Follows.Remove(existingFollow);
            }

            await _db.SaveChangesAsync();

            if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
                return Redirect($"/u/{username}");

            return Redirect(returnUrl);
        }
    }
}