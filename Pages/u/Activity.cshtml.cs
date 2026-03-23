using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.u
{
    public class ActivityModel : PageModel
    {
        private readonly AppDbContext _db;

        public ActivityModel(AppDbContext db)
        {
            _db = db;
        }

        public User ProfileUser { get; set; } = default!;
        public List<ActivityItemViewModel> ActivityItems { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string username, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(username))
                return NotFound();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
            if (user == null)
                return NotFound();

            ProfileUser = user;

            var activities = await _db.UserActivities
                .Where(x => x.UserId == user.Id)
                .Include(x => x.TargetUser)
                .OrderByDescending(x => x.CreatedAt)
                .Take(50)
                .ToListAsync(ct);

            ActivityItems = activities
                .Select(x => new ActivityItemViewModel
                {
                    Type = x.Type,
                    CreatedAt = x.CreatedAt,
                    TargetUsername = x.TargetUser?.Username,
                    TargetDisplayName = x.TargetUser?.DisplayName
                })
                .ToList();

            return Page();
        }

        public class ActivityItemViewModel
        {
            public string Type { get; set; } = "";
            public DateTime CreatedAt { get; set; }

            public string? TargetUsername { get; set; }
            public string? TargetDisplayName { get; set; }
        }
    }
}