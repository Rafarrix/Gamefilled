using System.Globalization;
using System.Text.Json;
using Gamefilled.Data;
using Gamefilled.Infrastructure;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.games;

public sealed class GameModel : PageModel
{
    private readonly IgdbClient _igdbClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _db;
    private readonly ILogger<GameModel> _logger;

    public GameModel(
        IgdbClient igdbClient,
        IHttpClientFactory httpClientFactory,
        AppDbContext db,
        ILogger<GameModel> logger)
    {
        _igdbClient = igdbClient;
        _httpClientFactory = httpClientFactory;
        _db = db;
        _logger = logger;
    }

    public IgdbGameDetailsDto? Game { get; private set; }
    public int? TtbNormallySeconds { get; private set; }
    public int? TtbCompletelySeconds { get; private set; }
    public int? TtbHastilySeconds { get; private set; }
    public string? BgUrl { get; private set; }
    public string? CoverUrl { get; private set; }

    public bool IsAuthenticated { get; private set; }
    public bool GamefilledDataAvailable { get; private set; } = true;
    public UserGameEntry? CurrentEntry { get; private set; }
    public IReadOnlyDictionary<string, int> CommunityStatusCounts { get; private set; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<GameFriendActivityViewModel> FriendActivity { get; private set; } = [];
    public IReadOnlyList<GameReleaseTimelineItem> ReleaseTimeline { get; private set; } = [];
    public IReadOnlyList<GameStoreLink> StoreLinks { get; private set; } = [];

    [TempData]
    public string? GameFeedback { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return NotFound();

        try
        {
            Game = await _igdbClient.GetGameDetailsAsync(id, cancellationToken);

            if (Game is null)
                return NotFound();

            await LoadTimeToBeatAsync(id, cancellationToken);
            await LoadGamefilledContextAsync(id, cancellationToken);

            ReleaseTimeline = BuildReleaseTimeline(Game.ReleaseDates);
            StoreLinks = BuildStoreLinks(Game.ExternalGames, Game.Websites);

            var backgroundImage = (Game.Artworks ?? [])
                .Concat(Game.Screenshots ?? [])
                .Where(image => !string.IsNullOrWhiteSpace(image.ImageId))
                .OrderByDescending(image =>
                    image.Width.HasValue &&
                    image.Height.HasValue &&
                    image.Width.Value >= image.Height.Value)
                .ThenByDescending(image =>
                    (long)(image.Width ?? 0) * (image.Height ?? 0))
                .FirstOrDefault();

            var backgroundImageId = backgroundImage?.ImageId ?? Game.Cover?.ImageId;

            BgUrl = BuildIgdbImage(backgroundImageId, "t_1080p_2x", "webp");
            CoverUrl = BuildIgdbImage(Game.Cover?.ImageId, "t_cover_big", "jpg");

            return Page();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load game {GameId}.", id);
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    public async Task<IActionResult> OnPostSetStatusAsync(
        int id,
        string? status,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = GameLibraryStatus.Normalize(status);
        if (id <= 0 || normalizedStatus is null)
            return BadRequest();

        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return RedirectToLogin(id);

        try
        {
            var entry = await _db.UserGameEntries
                .SingleOrDefaultAsync(
                    item => item.UserId == user.Id && item.GameId == id,
                    cancellationToken);

            if (entry is null)
            {
                entry = new UserGameEntry
                {
                    UserId = user.Id,
                    GameId = id,
                    Status = normalizedStatus,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _db.UserGameEntries.Add(entry);
            }
            else
            {
                entry.Status = normalizedStatus;
                entry.UpdatedAt = DateTime.UtcNow;
            }

            _db.UserActivities.Add(new UserActivity
            {
                UserId = user.Id,
                Type = "game_status_changed",
                GameId = id,
                MetaJson = JsonSerializer.Serialize(new { status = normalizedStatus }),
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
            GameFeedback = $"Added to {StatusLabel(normalizedStatus)}.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to update game status for user {UserId}.", user.Id);
            GameFeedback = "Game status could not be saved. Run the latest database upgrade if this is a new checkout.";
        }

        return RedirectToPage("/games/Game", new { id });
    }

    public async Task<IActionResult> OnPostSaveReviewAsync(
        int id,
        int? rating,
        string? reviewText,
        bool containsSpoilers,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest();

        if (rating is < 1 or > 10)
        {
            GameFeedback = "Rating must be between 1 and 10.";
            return RedirectToPage("/games/Game", new { id });
        }

        reviewText = reviewText?.Trim();
        if (reviewText?.Length > 5000)
        {
            GameFeedback = "Reviews can contain up to 5,000 characters.";
            return RedirectToPage("/games/Game", new { id });
        }

        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return RedirectToLogin(id);

        try
        {
            var entry = await _db.UserGameEntries
                .SingleOrDefaultAsync(
                    item => item.UserId == user.Id && item.GameId == id,
                    cancellationToken);

            if (entry is null)
            {
                entry = new UserGameEntry
                {
                    UserId = user.Id,
                    GameId = id,
                    Status = GameLibraryStatus.Played,
                    CreatedAt = DateTime.UtcNow
                };

                _db.UserGameEntries.Add(entry);
            }

            entry.Rating = rating;
            entry.ReviewText = string.IsNullOrWhiteSpace(reviewText) ? null : reviewText;
            entry.ContainsSpoilers = containsSpoilers;
            entry.UpdatedAt = DateTime.UtcNow;

            _db.UserActivities.Add(new UserActivity
            {
                UserId = user.Id,
                Type = string.IsNullOrWhiteSpace(entry.ReviewText) ? "rated_game" : "reviewed_game",
                GameId = id,
                MetaJson = JsonSerializer.Serialize(new { rating, containsSpoilers }),
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(cancellationToken);
            GameFeedback = string.IsNullOrWhiteSpace(entry.ReviewText)
                ? "Your rating was saved."
                : "Your review was saved.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to save review for user {UserId}.", user.Id);
            GameFeedback = "Your rating or review could not be saved.";
        }

        return RedirectToPage("/games/Game", new { id });
    }

    public async Task<IActionResult> OnPostRemoveEntryAsync(
        int id,
        CancellationToken cancellationToken)
    {
        if (id <= 0)
            return BadRequest();

        var user = await GetCurrentUserAsync(cancellationToken);
        if (user is null)
            return RedirectToLogin(id);

        try
        {
            var entry = await _db.UserGameEntries
                .SingleOrDefaultAsync(
                    item => item.UserId == user.Id && item.GameId == id,
                    cancellationToken);

            if (entry is not null)
            {
                _db.UserGameEntries.Remove(entry);
                await _db.SaveChangesAsync(cancellationToken);
            }

            GameFeedback = "Removed from your library.";
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to remove game entry for user {UserId}.", user.Id);
            GameFeedback = "The game could not be removed from your library.";
        }

        return RedirectToPage("/games/Game", new { id });
    }

    private async Task LoadGamefilledContextAsync(
        int gameId,
        CancellationToken cancellationToken)
    {
        try
        {
            var groupedCounts = await _db.UserGameEntries
                .AsNoTracking()
                .Where(item => item.GameId == gameId)
                .GroupBy(item => item.Status)
                .Select(group => new { Status = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);

            CommunityStatusCounts = groupedCounts.ToDictionary(
                item => item.Status,
                item => item.Count,
                StringComparer.OrdinalIgnoreCase);

            var username = HttpContext.Session.GetString("username");
            IsAuthenticated = !string.IsNullOrWhiteSpace(username);

            if (!IsAuthenticated)
                return;

            var currentUser = await _db.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(user => user.Username == username, cancellationToken);

            if (currentUser is null)
                return;

            CurrentEntry = await _db.UserGameEntries
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.UserId == currentUser.Id && item.GameId == gameId,
                    cancellationToken);

            var followingIds = await _db.Follows
                .AsNoTracking()
                .Where(follow => follow.FollowerId == currentUser.Id)
                .Select(follow => follow.FollowingId)
                .ToListAsync(cancellationToken);

            var followerIds = await _db.Follows
                .AsNoTracking()
                .Where(follow => follow.FollowingId == currentUser.Id)
                .Select(follow => follow.FollowerId)
                .ToListAsync(cancellationToken);

            var mutualIds = followingIds.Intersect(followerIds).ToArray();
            if (mutualIds.Length == 0)
                return;

            FriendActivity = await _db.UserGameEntries
                .AsNoTracking()
                .Where(item => item.GameId == gameId && mutualIds.Contains(item.UserId))
                .Include(item => item.User)
                .OrderByDescending(item => item.UpdatedAt)
                .Take(8)
                .Select(item => new GameFriendActivityViewModel
                {
                    Username = item.User!.Username ?? string.Empty,
                    DisplayName = item.User.DisplayName ?? item.User.Username ?? "User",
                    AvatarUrl = item.User.AvatarUrl,
                    Status = item.Status,
                    Rating = item.Rating
                })
                .ToListAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            GamefilledDataAvailable = false;
            CommunityStatusCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            FriendActivity = [];
            CurrentEntry = null;

            _logger.LogWarning(
                exception,
                "Personalized game data is unavailable. The UserGameEntries database upgrade may be missing.");
        }
    }

    private async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var username = HttpContext.Session.GetString("username");
        if (string.IsNullOrWhiteSpace(username))
            return null;

        return await _db.Users
            .SingleOrDefaultAsync(user => user.Username == username, cancellationToken);
    }

    private IActionResult RedirectToLogin(int gameId)
    {
        var returnUrl = Url.Page("/games/Game", new { id = gameId }) ?? $"/games/{gameId}";
        return RedirectToPage("/users/Login", new { returnUrl });
    }

    private async Task LoadTimeToBeatAsync(
        int gameId,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var relativeUrl = $"/api/igdbttb?id={gameId}";
            var requestUrl = client.BaseAddress is not null
                ? relativeUrl
                : $"{Request.Scheme}://{Request.Host}{relativeUrl}";

            using var response = await client.GetAsync(requestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return;

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            ParseTimeToBeat(responseBody);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(
                exception,
                "Unable to load time-to-beat data for game {GameId}.",
                gameId);
        }
    }

    private void ParseTimeToBeat(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return;

        var first = document.RootElement.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object)
            return;

        if (first.TryGetProperty("normally", out var normally) &&
            normally.ValueKind == JsonValueKind.Number)
        {
            TtbNormallySeconds = normally.GetInt32();
        }

        if (first.TryGetProperty("completely", out var completely) &&
            completely.ValueKind == JsonValueKind.Number)
        {
            TtbCompletelySeconds = completely.GetInt32();
        }

        if (first.TryGetProperty("hastily", out var hastily) &&
            hastily.ValueKind == JsonValueKind.Number)
        {
            TtbHastilySeconds = hastily.GetInt32();
        }
    }

    private static IReadOnlyList<GameReleaseTimelineItem> BuildReleaseTimeline(
        IEnumerable<IgdbReleaseDateDto>? releaseDates)
    {
        return (releaseDates ?? [])
            .Where(item => item.Platform is { Id: > 0 } && !string.IsNullOrWhiteSpace(item.Platform.Name))
            .Select(item =>
            {
                DateTimeOffset? date = null;

                if (item.DateUnix is > 0)
                {
                    try
                    {
                        date = DateTimeOffset.FromUnixTimeSeconds(item.DateUnix.Value);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        date = null;
                    }
                }

                var dateText = date?.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)
                    ?? item.Human
                    ?? item.Year?.ToString(CultureInfo.InvariantCulture)
                    ?? "Date TBA";

                return new GameReleaseTimelineItem(
                    item.Platform!.Id,
                    item.Platform.Name!,
                    date,
                    item.Year ?? date?.Year,
                    dateText);
            })
            .GroupBy(item => new
            {
                item.PlatformId,
                Date = item.Date?.UtcDateTime.Date,
                item.DateText
            })
            .Select(group => group.First())
            .OrderBy(item => item.Date ?? DateTimeOffset.MaxValue)
            .ThenBy(item => item.Year ?? int.MaxValue)
            .ThenBy(item => item.PlatformName, StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToList();
    }

    private static IReadOnlyList<GameStoreLink> BuildStoreLinks(
        IEnumerable<IgdbExternalGameDto>? externalGames,
        IEnumerable<IgdbWebsiteDto>? websites)
    {
        var links = new List<GameStoreLink>();

        foreach (var externalGame in externalGames ?? [])
        {
            if (!TryNormalizeExternalUrl(externalGame.Url, out var url))
                continue;

            var source = externalGame.Source?.Name?.Trim();
            var platform = externalGame.Platform?.Name?.Trim();
            var label = FirstNonEmpty(source, platform, externalGame.Name, HostLabel(url));

            links.Add(new GameStoreLink(
                label,
                url,
                StoreIcon(label),
                platform,
                IsStoreLabel(label)));
        }

        foreach (var website in websites ?? [])
        {
            if (!TryNormalizeExternalUrl(website.Url, out var url))
                continue;

            var type = website.Type?.Name?.Trim();
            var label = FirstNonEmpty(type, HostLabel(url));

            links.Add(new GameStoreLink(
                label,
                url,
                StoreIcon(label),
                null,
                IsStoreLabel(label) || string.Equals(label, "Official", StringComparison.OrdinalIgnoreCase)));
        }

        return links
            .Where(link => link.IsRelevant)
            .GroupBy(link => link.Url, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(link => link.IsStore)
            .ThenBy(link => StoreSortKey(link.Label))
            .ThenBy(link => link.Label, StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }

    private static bool TryNormalizeExternalUrl(string? value, out string url)
    {
        url = string.Empty;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return false;
        }

        url = uri.ToString();
        return true;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "Website";

    private static string HostLabel(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.Host.Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase)
            : "Website";

    private static bool IsStoreLabel(string value)
    {
        var label = value.ToLowerInvariant();
        return label.Contains("steam") ||
               label.Contains("gog") ||
               label.Contains("epic") ||
               label.Contains("playstation") ||
               label.Contains("xbox") ||
               label.Contains("microsoft") ||
               label.Contains("nintendo") ||
               label.Contains("itch") ||
               label.Contains("android") ||
               label.Contains("apple") ||
               label.Contains("app store");
    }

    private static int StoreSortKey(string value)
    {
        var label = value.ToLowerInvariant();
        if (label.Contains("steam")) return 1;
        if (label.Contains("playstation")) return 2;
        if (label.Contains("xbox") || label.Contains("microsoft")) return 3;
        if (label.Contains("nintendo")) return 4;
        if (label.Contains("epic")) return 5;
        if (label.Contains("gog")) return 6;
        if (label.Contains("itch")) return 7;
        if (label.Contains("android")) return 8;
        if (label.Contains("apple") || label.Contains("app store")) return 9;
        if (label.Contains("official")) return 20;
        return 50;
    }

    private static string StoreIcon(string value)
    {
        var label = value.ToLowerInvariant();
        if (label.Contains("steam")) return "fab fa-steam";
        if (label.Contains("playstation")) return "fab fa-playstation";
        if (label.Contains("xbox") || label.Contains("microsoft")) return "fab fa-xbox";
        if (label.Contains("android")) return "fab fa-android";
        if (label.Contains("apple") || label.Contains("app store")) return "fab fa-apple";
        if (label.Contains("youtube")) return "fab fa-youtube";
        return "fas fa-arrow-up-right-from-square";
    }

    private static string StatusLabel(string status) => status switch
    {
        GameLibraryStatus.Played => "Played",
        GameLibraryStatus.Playing => "Playing",
        GameLibraryStatus.Wishlist => "Wishlist",
        _ => "Backlog"
    };

    private static string? BuildIgdbImage(string? imageId, string size, string extension) =>
        string.IsNullOrWhiteSpace(imageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/{size}/{imageId}.{extension}";
}

public sealed record GameReleaseTimelineItem(
    int PlatformId,
    string PlatformName,
    DateTimeOffset? Date,
    int? Year,
    string DateText);

public sealed record GameStoreLink(
    string Label,
    string Url,
    string Icon,
    string? Platform,
    bool IsStore)
{
    public bool IsRelevant => IsStore ||
        Label.Contains("official", StringComparison.OrdinalIgnoreCase);
}

public sealed class GameFriendActivityViewModel
{
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string Status { get; init; } = string.Empty;
    public int? Rating { get; init; }
}
