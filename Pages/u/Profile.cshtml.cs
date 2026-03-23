using Gamefilled.Data;
using Gamefilled.Infrastructure;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.u
{
    public class ProfileModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IgdbClient _igdb;

        public ProfileModel(AppDbContext db, IgdbClient igdb)
        {
            _db = db;
            _igdb = igdb;
        }

        public User ProfileUser { get; set; } = default!;

        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }

        public bool IsOwnProfile { get; set; }
        public bool IsFollowing { get; set; }

        public string ActiveTab { get; set; } = "profile";

        public string MemberSinceText { get; set; } = "";
        public bool IsOnlineNow { get; set; }
        public string OnlineStatusText { get; set; } = "";

        public List<ProfileFavoriteGameViewModel> FavoriteGames { get; set; } = new();
        public List<ProfileFollowUserViewModel> FollowersUsers { get; set; } = new();
        public List<ProfileFollowUserViewModel> FollowingUsers { get; set; } = new();
        public List<ProfileActivityItemViewModel> ActivityItems { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(string username, string? tab, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(username))
                return NotFound();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
            if (user == null)
                return NotFound();

            ProfileUser = user;

            ActiveTab = NormalizeTab(tab);

            var currentUsername = HttpContext.Session.GetString("username");
            IsOwnProfile = currentUsername == username;

            FollowersCount = await _db.Follows.CountAsync(f => f.FollowingId == ProfileUser.Id, ct);
            FollowingCount = await _db.Follows.CountAsync(f => f.FollowerId == ProfileUser.Id, ct);

            if (!string.IsNullOrWhiteSpace(currentUsername) && !IsOwnProfile)
            {
                var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == currentUsername, ct);
                if (currentUser != null)
                {
                    IsFollowing = await _db.Follows.AnyAsync(f =>
                        f.FollowerId == currentUser.Id &&
                        f.FollowingId == ProfileUser.Id, ct);
                }
            }

            MemberSinceText = ProfileUser.CreatedAt.ToString("MM/yyyy");
            BuildOnlineStatus(ProfileUser.LastSeenAt);

            await LoadFavoriteGamesAsync(ct);
            await LoadActivityAsync(ct);

            if (ActiveTab == "followers")
                await LoadFollowersAsync(ct);

            if (ActiveTab == "following")
                await LoadFollowingAsync(ct);

            return Page();
        }

        private async Task LoadFavoriteGamesAsync(CancellationToken ct)
        {
            var favoriteRows = await _db.UserFavoriteGames
                .Where(x => x.UserId == ProfileUser.Id)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(ct);

            var gameIds = favoriteRows
                .Select(x => x.GameId)
                .ToList();

            var igdbGames = await _igdb.GetGamesByIdsAsync(gameIds, ct);
            var byId = igdbGames.ToDictionary(x => x.Id, x => x);

            FavoriteGames = favoriteRows
                .Select(x =>
                {
                    byId.TryGetValue(x.GameId, out var game);

                    return new ProfileFavoriteGameViewModel
                    {
                        GameId = x.GameId,
                        SortOrder = x.SortOrder,
                        IsPrimary = x.IsPrimary,
                        Name = game?.Name,
                        CoverUrl = string.IsNullOrWhiteSpace(game?.Cover?.ImageId)
                            ? null
                            : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{game.Cover.ImageId}.jpg"
                    };
                })
                .OrderBy(x => x.SortOrder)
                .ToList();
        }

        private async Task LoadFollowersAsync(CancellationToken ct)
        {
            var profileFollowingIds = (await _db.Follows
                .Where(f => f.FollowerId == ProfileUser.Id)
                .Select(f => f.FollowingId)
                .ToListAsync(ct))
                .ToHashSet();

            var followers = await _db.Follows
                .Where(f => f.FollowingId == ProfileUser.Id)
                .Include(f => f.Follower)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Follower!)
                .ToListAsync(ct);

            FollowersUsers = followers
                .Select(u => new ProfileFollowUserViewModel
                {
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    AvatarUrl = u.AvatarUrl,
                    IsOnlineNow = IsUserOnline(u.LastSeenAt),
                    StatusText = IsUserOnline(u.LastSeenAt) ? "Online" : "Offline",
                    IsFriend = profileFollowingIds.Contains(u.Id)
                })
                .ToList();
        }

        private async Task LoadFollowingAsync(CancellationToken ct)
        {
            var profileFollowerIds = (await _db.Follows
                .Where(f => f.FollowingId == ProfileUser.Id)
                .Select(f => f.FollowerId)
                .ToListAsync(ct))
                .ToHashSet();

            var following = await _db.Follows
                .Where(f => f.FollowerId == ProfileUser.Id)
                .Include(f => f.Following)
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => f.Following!)
                .ToListAsync(ct);

            FollowingUsers = following
                .Select(u => new ProfileFollowUserViewModel
                {
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    AvatarUrl = u.AvatarUrl,
                    IsOnlineNow = IsUserOnline(u.LastSeenAt),
                    StatusText = IsUserOnline(u.LastSeenAt) ? "Online" : "Offline",
                    IsFriend = profileFollowerIds.Contains(u.Id)
                })
                .ToList();
        }

        private async Task LoadActivityAsync(CancellationToken ct)
        {
            var activities = await _db.UserActivities
                .Where(x => x.UserId == ProfileUser.Id)
                .Include(x => x.TargetUser)
                .OrderByDescending(x => x.CreatedAt)
                .Take(20)
                .ToListAsync(ct);

            ActivityItems = activities
                .Select(x => new ProfileActivityItemViewModel
                {
                    Type = x.Type,
                    CreatedAt = x.CreatedAt,
                    TargetUsername = x.TargetUser?.Username,
                    TargetDisplayName = x.TargetUser?.DisplayName
                })
                .ToList();
        }

        private static string NormalizeTab(string? tab)
        {
            var value = (tab ?? "profile").Trim().ToLowerInvariant();

            return value switch
            {
                "profile" => "profile",
                "activity" => "activity",
                "reviews" => "reviews",
                "lists" => "lists",
                "following" => "following",
                "followers" => "followers",
                _ => "profile"
            };
        }

        private void BuildOnlineStatus(DateTime? lastSeenAt)
        {
            if (lastSeenAt == null)
            {
                IsOnlineNow = false;
                OnlineStatusText = "Offline";
                return;
            }

            var now = DateTime.UtcNow;
            var diff = now - lastSeenAt.Value;

            if (diff <= TimeSpan.FromMinutes(5))
            {
                IsOnlineNow = true;
                OnlineStatusText = "Online";
                return;
            }

            IsOnlineNow = false;
            OnlineStatusText = "Offline";
        }

        private static bool IsUserOnline(DateTime? lastSeenAt)
        {
            if (lastSeenAt == null) return false;
            return (DateTime.UtcNow - lastSeenAt.Value) <= TimeSpan.FromMinutes(5);
        }
    }

    public class ProfileFollowUserViewModel
    {
        public string? Username { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }

        public bool IsOnlineNow { get; set; }
        public string StatusText { get; set; } = "Offline";

        public bool IsFriend { get; set; }
    }

    public class ProfileActivityItemViewModel
    {
        public string Type { get; set; } = "";
        public DateTime CreatedAt { get; set; }

        public string? TargetUsername { get; set; }
        public string? TargetDisplayName { get; set; }
    }
}