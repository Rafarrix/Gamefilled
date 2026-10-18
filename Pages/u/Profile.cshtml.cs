using System.Text.Json;
using Gamefilled.Application.Social;
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
        private readonly SocialGraphService _socialGraph;

        private SocialGraphSnapshot _profileGraph = SocialGraphSnapshot.Empty;

        public ProfileModel(AppDbContext db, IgdbClient igdb, SocialGraphService socialGraph)
        {
            _db = db;
            _igdb = igdb;
            _socialGraph = socialGraph;
        }

        public User ProfileUser { get; set; } = default!;
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public int MutualConnectionsCount { get; set; }
        public bool IsOwnProfile { get; set; }
        public bool IsFollowing { get; set; }
        public bool IsMutualWithViewer { get; set; }
        public string ActiveTab { get; set; } = "profile";
        public string MemberSinceText { get; set; } = string.Empty;
        public bool IsOnlineNow { get; set; }
        public string OnlineStatusText { get; set; } = "Offline";

        public int TotalTrackedGames { get; set; }
        public int ReviewCount { get; set; }
        public double? AverageRating { get; set; }
        public IReadOnlyDictionary<string, int> LibraryCounts { get; private set; } =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public List<ProfileFavoriteGameViewModel> FavoriteGames { get; set; } = new();
        public List<ProfileFollowUserViewModel> FollowersUsers { get; set; } = new();
        public List<ProfileFollowUserViewModel> FollowingUsers { get; set; } = new();
        public List<ProfileActivityItemViewModel> ActivityItems { get; set; } = new();
        public List<ProfileGameEntryViewModel> RecentGames { get; set; } = new();
        public List<ProfileGameEntryViewModel> RecentReviews { get; set; } = new();

        public int LibraryCount(string status) =>
            LibraryCounts.TryGetValue(status, out var count) ? count : 0;

        public async Task<IActionResult> OnGetAsync(string username, string? tab, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(username))
                return NotFound();

            var user = await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Username == username, ct);

            if (user == null)
                return NotFound();

            ProfileUser = user;
            ActiveTab = NormalizeTab(tab);

            var currentUsername = HttpContext.Session.GetString("username");
            IsOwnProfile = string.Equals(currentUsername, username, StringComparison.OrdinalIgnoreCase);

            _profileGraph = await _socialGraph.GetSnapshotAsync(ProfileUser.Id, ct);
            FollowersCount = _profileGraph.FollowerIds.Count;
            FollowingCount = _profileGraph.FollowingIds.Count;
            MutualConnectionsCount = _profileGraph.MutualIds.Count;

            if (!string.IsNullOrWhiteSpace(currentUsername) && !IsOwnProfile)
            {
                var currentUser = await _db.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Username == currentUsername, ct);

                if (currentUser != null)
                {
                    var viewerGraph = await _socialGraph.GetSnapshotAsync(currentUser.Id, ct);
                    IsFollowing = viewerGraph.FollowingIds.Contains(ProfileUser.Id);
                    IsMutualWithViewer = viewerGraph.MutualIds.Contains(ProfileUser.Id);
                }
            }

            MemberSinceText = ProfileUser.CreatedAt.ToString("MMMM yyyy");
            var utcNow = DateTime.UtcNow;
            IsOnlineNow = UserPresence.IsOnline(ProfileUser.LastSeenAt, utcNow);
            OnlineStatusText = UserPresence.Describe(ProfileUser.LastSeenAt, utcNow);

            await LoadFavoriteGamesAsync(ct);
            await LoadLibraryAsync(ct);
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
                .AsNoTracking()
                .Where(item => item.UserId == ProfileUser.Id)
                .OrderBy(item => item.SortOrder)
                .ToListAsync(ct);

            var gameIds = favoriteRows.Select(item => item.GameId).ToList();
            var igdbGames = await _igdb.GetGamesByIdsAsync(gameIds, ct);
            var byId = igdbGames.ToDictionary(item => item.Id, item => item);

            FavoriteGames = favoriteRows
                .Select(item =>
                {
                    byId.TryGetValue(item.GameId, out var game);
                    return new ProfileFavoriteGameViewModel
                    {
                        GameId = item.GameId,
                        SortOrder = item.SortOrder,
                        IsPrimary = item.IsPrimary,
                        Name = game?.Name,
                        CoverUrl = BuildCoverUrl(game?.Cover?.ImageId)
                    };
                })
                .OrderBy(item => item.SortOrder)
                .ToList();
        }

        private async Task LoadLibraryAsync(CancellationToken ct)
        {
            var entries = await _db.UserGameEntries
                .AsNoTracking()
                .Where(item => item.UserId == ProfileUser.Id)
                .OrderByDescending(item => item.UpdatedAt)
                .ToListAsync(ct);

            TotalTrackedGames = entries.Count;
            ReviewCount = entries.Count(item => !string.IsNullOrWhiteSpace(item.ReviewText));
            AverageRating = entries.Where(item => item.Rating.HasValue)
                .Select(item => (double?)item.Rating)
                .Average();

            LibraryCounts = entries
                .GroupBy(item => item.Status, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

            var recentRows = entries.Take(8).ToList();
            var reviewRows = entries
                .Where(item => !string.IsNullOrWhiteSpace(item.ReviewText))
                .Take(12)
                .ToList();

            var gameIds = recentRows
                .Concat(reviewRows)
                .Select(item => item.GameId)
                .Distinct()
                .ToArray();

            var games = await _igdb.GetGamesByIdsAsync(gameIds, ct);
            var gamesById = games.ToDictionary(item => item.Id, item => item);

            RecentGames = recentRows
                .Select(item => MapGameEntry(item, gamesById))
                .ToList();

            RecentReviews = reviewRows
                .Select(item => MapGameEntry(item, gamesById))
                .ToList();
        }

        private async Task LoadFollowersAsync(CancellationToken ct)
        {
            var users = await _db.Users
                .AsNoTracking()
                .Where(user => _profileGraph.FollowerIds.Contains(user.Id))
                .ToListAsync(ct);

            FollowersUsers = BuildFollowUsers(users);
        }

        private async Task LoadFollowingAsync(CancellationToken ct)
        {
            var users = await _db.Users
                .AsNoTracking()
                .Where(user => _profileGraph.FollowingIds.Contains(user.Id))
                .ToListAsync(ct);

            FollowingUsers = BuildFollowUsers(users);
        }

        private List<ProfileFollowUserViewModel> BuildFollowUsers(IEnumerable<User> users)
        {
            var utcNow = DateTime.UtcNow;

            return users
                .Select(user => new ProfileFollowUserViewModel
                {
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    AvatarUrl = user.AvatarUrl,
                    IsOnlineNow = UserPresence.IsOnline(user.LastSeenAt, utcNow),
                    StatusText = UserPresence.Describe(user.LastSeenAt, utcNow),
                    IsFriend = _profileGraph.MutualIds.Contains(user.Id)
                })
                .OrderByDescending(user => user.IsOnlineNow)
                .ThenBy(user => user.DisplayName ?? user.Username)
                .ToList();
        }

        private async Task LoadActivityAsync(CancellationToken ct)
        {
            var activities = await _db.UserActivities
                .AsNoTracking()
                .Where(item => item.UserId == ProfileUser.Id)
                .Include(item => item.TargetUser)
                .OrderByDescending(item => item.CreatedAt)
                .Take(30)
                .ToListAsync(ct);

            var gameIds = activities
                .Where(item => item.GameId.HasValue)
                .Select(item => item.GameId!.Value)
                .Distinct()
                .ToArray();

            var games = await _igdb.GetGamesByIdsAsync(gameIds, ct);
            var gamesById = games.ToDictionary(item => item.Id, item => item);

            ActivityItems = activities
                .Select(item =>
                {
                    IgdbGameDto? game = null;
                    if (item.GameId.HasValue)
                        gamesById.TryGetValue(item.GameId.Value, out game);

                    return new ProfileActivityItemViewModel
                    {
                        Type = item.Type,
                        CreatedAt = item.CreatedAt,
                        TargetUsername = item.TargetUser?.Username,
                        TargetDisplayName = item.TargetUser?.DisplayName,
                        GameId = item.GameId,
                        GameName = game?.Name,
                        GameCoverUrl = BuildCoverUrl(game?.Cover?.ImageId),
                        Status = ReadMetaString(item.MetaJson, "status"),
                        Rating = ReadMetaInt(item.MetaJson, "rating")
                    };
                })
                .ToList();
        }

        private static ProfileGameEntryViewModel MapGameEntry(
            UserGameEntry entry,
            IReadOnlyDictionary<int, IgdbGameDto> gamesById)
        {
            gamesById.TryGetValue(entry.GameId, out var game);

            return new ProfileGameEntryViewModel
            {
                GameId = entry.GameId,
                Name = game?.Name ?? $"Game {entry.GameId}",
                CoverUrl = BuildCoverUrl(game?.Cover?.ImageId),
                Status = entry.Status,
                Rating = entry.Rating,
                ReviewText = entry.ReviewText,
                ContainsSpoilers = entry.ContainsSpoilers,
                UpdatedAt = entry.UpdatedAt
            };
        }

        private static string? BuildCoverUrl(string? imageId) =>
            string.IsNullOrWhiteSpace(imageId)
                ? null
                : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{imageId}.jpg";

        private static string? ReadMetaString(string? json, string property)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var document = JsonDocument.Parse(json);
                return document.RootElement.TryGetProperty(property, out var value) &&
                       value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static int? ReadMetaInt(string? json, string property)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var document = JsonDocument.Parse(json);
                return document.RootElement.TryGetProperty(property, out var value) &&
                       value.ValueKind == JsonValueKind.Number &&
                       value.TryGetInt32(out var result)
                    ? result
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
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
        public string Type { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? TargetUsername { get; set; }
        public string? TargetDisplayName { get; set; }
        public int? GameId { get; set; }
        public string? GameName { get; set; }
        public string? GameCoverUrl { get; set; }
        public string? Status { get; set; }
        public int? Rating { get; set; }
    }

    public class ProfileGameEntryViewModel
    {
        public int GameId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public string Status { get; set; } = GameLibraryStatus.Backlog;
        public int? Rating { get; set; }
        public string? ReviewText { get; set; }
        public bool ContainsSpoilers { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
