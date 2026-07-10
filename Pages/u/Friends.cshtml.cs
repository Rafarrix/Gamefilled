using Gamefilled.Application.Social;
using Gamefilled.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.u
{
    /// <summary>
    /// Displays mutual relationships for a profile.
    /// A friend is someone the profile follows who also follows the profile back.
    /// </summary>
    public class FriendsModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly SocialGraphService _socialGraph;

        public FriendsModel(AppDbContext db, SocialGraphService socialGraph)
        {
            _db = db;
            _socialGraph = socialGraph;
        }

        public Models.User ProfileUser { get; set; } = default!;
        public List<FriendUserViewModel> Friends { get; set; } = new();
        public bool IsOwnProfile { get; private set; }

        public async Task<IActionResult> OnGetAsync(string username, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(username))
                return NotFound();

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Username == username, ct);

            if (user == null)
                return NotFound();

            ProfileUser = user;

            var currentUsername = HttpContext.Session.GetString("username");
            IsOwnProfile = !string.IsNullOrWhiteSpace(currentUsername) &&
                string.Equals(currentUsername, user.Username, StringComparison.OrdinalIgnoreCase);

            var graph = await _socialGraph.GetSnapshotAsync(user.Id, ct);
            if (graph.MutualIds.Count == 0)
            {
                Friends = [];
                return Page();
            }

            var utcNow = DateTime.UtcNow;
            Friends = await _db.Users
                .AsNoTracking()
                .Where(friend => graph.MutualIds.Contains(friend.Id))
                .OrderByDescending(friend =>
                    friend.LastSeenAt != null &&
                    friend.LastSeenAt >= UserPresence.OnlineSince(utcNow) &&
                    friend.LastSeenAt <= UserPresence.FutureLimit(utcNow))
                .ThenBy(friend => friend.DisplayName ?? friend.Username)
                .Select(friend => new FriendUserViewModel
                {
                    Username = friend.Username,
                    DisplayName = friend.DisplayName,
                    AvatarUrl = friend.AvatarUrl,
                    LastSeenAt = friend.LastSeenAt
                })
                .ToListAsync(ct);

            foreach (var friend in Friends)
            {
                friend.IsOnlineNow = UserPresence.IsOnline(friend.LastSeenAt, utcNow);
                friend.PresenceText = UserPresence.Describe(friend.LastSeenAt, utcNow);
            }

            return Page();
        }

        public class FriendUserViewModel
        {
            public string? Username { get; set; }
            public string? DisplayName { get; set; }
            public string? AvatarUrl { get; set; }
            public DateTime? LastSeenAt { get; set; }
            public bool IsOnlineNow { get; set; }
            public string PresenceText { get; set; } = "Offline";
        }
    }
}
