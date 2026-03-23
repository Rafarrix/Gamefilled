using Gamefilled.Data;
using Gamefilled.Infrastructure;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.Settings
{
    public class FavoriteGamesModel : PageModel
    {
        private readonly AppDbContext _db;
        private readonly IgdbClient _igdb;

        public FavoriteGamesModel(AppDbContext db, IgdbClient igdb)
        {
            _db = db;
            _igdb = igdb;
        }

        public string? CurrentUsername { get; set; }

        [BindProperty]
        public string? PayloadJson { get; set; }

        public List<FavoriteGameCardVm> FavoriteGames { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(CancellationToken ct)
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToPage("/users/Login");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
            if (user == null)
                return RedirectToPage("/");

            CurrentUsername = user.Username;

            await LoadFavoritesAsync(user.Id, ct);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(CancellationToken ct)
        {
            var username = HttpContext.Session.GetString("username");
            if (string.IsNullOrWhiteSpace(username))
                return RedirectToPage("/users/Login");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
            if (user == null)
                return RedirectToPage("/");

            CurrentUsername = user.Username;

            var parsed = ParsePayload(PayloadJson);

            if (parsed.Count > 5)
            {
                ModelState.AddModelError(string.Empty, "You can only have up to 5 favorite games.");
                await LoadFavoritesAsync(user.Id, ct);
                return Page();
            }

            var distinctIds = parsed.Select(x => x.GameId).Distinct().ToList();
            if (distinctIds.Count != parsed.Count)
            {
                ModelState.AddModelError(string.Empty, "You cannot repeat the same game in your favorites.");
                await LoadFavoritesFromParsedAsync(parsed, ct);
                return Page();
            }

            if (parsed.Count(x => x.IsPrimary) > 1)
            {
                ModelState.AddModelError(string.Empty, "Only one game can be highlighted as your top favorite.");
                await LoadFavoritesFromParsedAsync(parsed, ct);
                return Page();
            }

            if (parsed.Count > 0 && parsed.All(x => !x.IsPrimary))
            {
                parsed[0].IsPrimary = true;
            }

            var existing = await _db.UserFavoriteGames
                .Where(x => x.UserId == user.Id)
                .ToListAsync(ct);

            _db.UserFavoriteGames.RemoveRange(existing);

            var rows = parsed
                .Select((x, index) => new UserFavoriteGame
                {
                    UserId = user.Id,
                    GameId = x.GameId,
                    SortOrder = index + 1,
                    IsPrimary = x.IsPrimary,
                    CreatedAt = DateTime.UtcNow
                })
                .ToList();

            if (rows.Any())
                _db.UserFavoriteGames.AddRange(rows);

            _db.UserActivities.Add(new UserActivity
            {
                UserId = user.Id,
                Type = "updated_favorites",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);

            return Redirect($"/u/{username}");
        }

        private async Task LoadFavoritesAsync(int userId, CancellationToken ct)
        {
            var rows = await _db.UserFavoriteGames
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(ct);

            var ids = rows.Select(x => x.GameId).Distinct().ToList();
            var igdbGames = await _igdb.GetGamesByIdsAsync(ids, ct);
            var byId = igdbGames.ToDictionary(x => x.Id, x => x);

            FavoriteGames = rows
                .Select(x => BuildCardVm(x.GameId, x.SortOrder, x.IsPrimary, byId))
                .OrderBy(x => x.SortOrder)
                .ToList();

            PayloadJson = System.Text.Json.JsonSerializer.Serialize(
                FavoriteGames.Select(x => new FavoritePayloadItem
                {
                    GameId = x.GameId,
                    IsPrimary = x.IsPrimary
                }).ToList(),
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                });
        }

        private async Task LoadFavoritesFromParsedAsync(List<FavoritePayloadItem> parsed, CancellationToken ct)
        {
            var ids = parsed.Select(x => x.GameId).Distinct().ToList();
            var igdbGames = await _igdb.GetGamesByIdsAsync(ids, ct);
            var byId = igdbGames.ToDictionary(x => x.Id, x => x);

            FavoriteGames = parsed
                .Select((x, index) => BuildCardVm(x.GameId, index + 1, x.IsPrimary, byId))
                .OrderBy(x => x.SortOrder)
                .ToList();

            PayloadJson = System.Text.Json.JsonSerializer.Serialize(
                parsed,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNamingPolicy = null
                });
        }

        private static FavoriteGameCardVm BuildCardVm(
            int gameId,
            int sortOrder,
            bool isPrimary,
            Dictionary<int, IgdbGameDto> byId)
        {
            byId.TryGetValue(gameId, out var game);

            return new FavoriteGameCardVm
            {
                GameId = gameId,
                SortOrder = sortOrder,
                IsPrimary = isPrimary,
                Name = game?.Name,
                Year = GetYear(game?.FirstReleaseDateUnix),
                CoverUrl = string.IsNullOrWhiteSpace(game?.Cover?.ImageId)
                    ? null
                    : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{game.Cover.ImageId}.jpg"
            };
        }

        private static int? GetYear(long? unix)
        {
            if (unix == null) return null;

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unix.Value).Year;
            }
            catch
            {
                return null;
            }
        }

        private static List<FavoritePayloadItem> ParsePayload(string? payloadJson)
        {
            if (string.IsNullOrWhiteSpace(payloadJson))
                return new List<FavoritePayloadItem>();

            try
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var parsed = System.Text.Json.JsonSerializer.Deserialize<List<FavoritePayloadItem>>(payloadJson, options);

                return parsed?
                    .Where(x => x.GameId > 0)
                    .ToList() ?? new List<FavoritePayloadItem>();
            }
            catch
            {
                return new List<FavoritePayloadItem>();
            }
        }

        public class FavoritePayloadItem
        {
            public int GameId { get; set; }
            public bool IsPrimary { get; set; }
        }

        public class FavoriteGameCardVm
        {
            public int GameId { get; set; }
            public int SortOrder { get; set; }
            public bool IsPrimary { get; set; }
            public string? Name { get; set; }
            public int? Year { get; set; }
            public string? CoverUrl { get; set; }
        }
    }
}