using System.Text.Json;
using Gamefilled.Data;
using Gamefilled.Infrastructure;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.u;

public sealed class ActivityModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IgdbClient _igdb;

    public ActivityModel(AppDbContext db, IgdbClient igdb)
    {
        _db = db;
        _igdb = igdb;
    }

    public User ProfileUser { get; private set; } = default!;
    public List<ActivityItemViewModel> ActivityItems { get; private set; } = [];
    public bool IsOwnProfile { get; private set; }

    public async Task<IActionResult> OnGetAsync(string username, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
            return NotFound();

        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Username == username, cancellationToken);

        if (user is null)
            return NotFound();

        ProfileUser = user;

        var currentUsername = HttpContext.Session.GetString("username");
        IsOwnProfile = !string.IsNullOrWhiteSpace(currentUsername) &&
            string.Equals(currentUsername, user.Username, StringComparison.OrdinalIgnoreCase);

        var activities = await _db.UserActivities
            .AsNoTracking()
            .Where(item => item.UserId == user.Id)
            .Include(item => item.TargetUser)
            .OrderByDescending(item => item.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        var gameIds = activities
            .Where(item => item.GameId.HasValue)
            .Select(item => item.GameId!.Value)
            .Distinct()
            .ToArray();

        var games = await _igdb.GetGamesByIdsAsync(gameIds, cancellationToken);
        var gamesById = games.ToDictionary(item => item.Id, item => item);

        ActivityItems = activities
            .Select(item =>
            {
                IgdbGameDto? game = null;
                if (item.GameId.HasValue)
                    gamesById.TryGetValue(item.GameId.Value, out game);

                return new ActivityItemViewModel
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

        return Page();
    }

    private static string? BuildCoverUrl(string? imageId) =>
        string.IsNullOrWhiteSpace(imageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/t_cover_small/{imageId}.jpg";

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

    public sealed class ActivityItemViewModel
    {
        public string Type { get; init; } = string.Empty;
        public DateTime CreatedAt { get; init; }
        public string? TargetUsername { get; init; }
        public string? TargetDisplayName { get; init; }
        public int? GameId { get; init; }
        public string? GameName { get; init; }
        public string? GameCoverUrl { get; init; }
        public string? Status { get; init; }
        public int? Rating { get; init; }
    }
}
