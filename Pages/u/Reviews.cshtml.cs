using Gamefilled.Data;
using Gamefilled.Infrastructure;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.u;

public sealed class ReviewsModel : PageModel
{
    private const int PageSize = 12;

    private readonly AppDbContext _db;
    private readonly IgdbClient _igdb;

    public ReviewsModel(AppDbContext db, IgdbClient igdb)
    {
        _db = db;
        _igdb = igdb;
    }

    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Query { get; set; }

    [BindProperty(SupportsGet = true, Name = "sort")]
    public string Sort { get; set; } = "recent";

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public User Owner { get; private set; } = default!;
    public bool IsOwnProfile { get; private set; }
    public IReadOnlyList<UserReviewCard> Reviews { get; private set; } = [];
    public int TotalReviews { get; private set; }
    public int TotalPages { get; private set; }
    public double? AverageRating { get; private set; }

    public async Task<IActionResult> OnGetAsync(string username, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
            return NotFound();

        var owner = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Username == username, cancellationToken);

        if (owner is null)
            return NotFound();

        Owner = owner;
        Query = Query?.Trim();
        Sort = NormalizeSort(Sort);
        PageNumber = Math.Max(1, PageNumber);

        var currentUsername = HttpContext.Session.GetString("username");
        IsOwnProfile = !string.IsNullOrWhiteSpace(currentUsername) &&
            string.Equals(currentUsername, Owner.Username, StringComparison.OrdinalIgnoreCase);

        var entries = await _db.UserGameEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == Owner.Id && entry.ReviewText != null && entry.ReviewText != "")
            .OrderByDescending(entry => entry.UpdatedAt)
            .ToListAsync(cancellationToken);

        AverageRating = entries
            .Where(entry => entry.Rating.HasValue)
            .Select(entry => (double?)entry.Rating)
            .Average();

        var games = await _igdb.GetGamesByIdsAsync(
            entries.Select(entry => entry.GameId).Distinct().ToArray(),
            cancellationToken);

        var gamesById = games.ToDictionary(game => game.Id, game => game);

        var cards = entries
            .Select(entry =>
            {
                gamesById.TryGetValue(entry.GameId, out var game);

                return new UserReviewCard
                {
                    GameId = entry.GameId,
                    GameName = game?.Name ?? $"Game {entry.GameId}",
                    CoverUrl = BuildCoverUrl(game?.Cover?.ImageId),
                    ReleaseYear = game?.FirstReleaseDate.HasValue == true
                        ? DateTimeOffset.FromUnixTimeSeconds(game.FirstReleaseDate.Value).Year
                        : null,
                    Status = entry.Status,
                    Rating = entry.Rating,
                    ReviewText = entry.ReviewText ?? string.Empty,
                    ContainsSpoilers = entry.ContainsSpoilers,
                    UpdatedAt = entry.UpdatedAt
                };
            });

        if (!string.IsNullOrWhiteSpace(Query))
        {
            cards = cards.Where(card =>
                card.GameName.Contains(Query, StringComparison.OrdinalIgnoreCase) ||
                card.ReviewText.Contains(Query, StringComparison.OrdinalIgnoreCase));
        }

        cards = Sort switch
        {
            "rating" => cards
                .OrderByDescending(card => card.Rating.HasValue)
                .ThenByDescending(card => card.Rating)
                .ThenByDescending(card => card.UpdatedAt),
            "title" => cards
                .OrderBy(card => card.GameName, StringComparer.OrdinalIgnoreCase),
            _ => cards.OrderByDescending(card => card.UpdatedAt)
        };

        var materialized = cards.ToList();
        TotalReviews = materialized.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalReviews / (double)PageSize));
        PageNumber = Math.Min(PageNumber, TotalPages);

        Reviews = materialized
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        return Page();
    }

    public string PageUrl(int page)
    {
        var values = new List<string>();

        if (!string.IsNullOrWhiteSpace(Query))
            values.Add($"q={Uri.EscapeDataString(Query)}");

        if (!string.Equals(Sort, "recent", StringComparison.OrdinalIgnoreCase))
            values.Add($"sort={Uri.EscapeDataString(Sort)}");

        if (page > 1)
            values.Add($"page={page}");

        var suffix = values.Count == 0 ? string.Empty : $"?{string.Join('&', values)}";
        return $"/u/{Uri.EscapeDataString(Owner.Username ?? string.Empty)}/reviews{suffix}";
    }

    public string SortUrl(string sort)
    {
        var normalized = NormalizeSort(sort);
        var values = new List<string>();

        if (!string.IsNullOrWhiteSpace(Query))
            values.Add($"q={Uri.EscapeDataString(Query)}");

        if (!string.Equals(normalized, "recent", StringComparison.OrdinalIgnoreCase))
            values.Add($"sort={Uri.EscapeDataString(normalized)}");

        var suffix = values.Count == 0 ? string.Empty : $"?{string.Join('&', values)}";
        return $"/u/{Uri.EscapeDataString(Owner.Username ?? string.Empty)}/reviews{suffix}";
    }

    public static string StatusLabel(string status) => status switch
    {
        GameLibraryStatus.Played => "Played",
        GameLibraryStatus.Playing => "Playing",
        GameLibraryStatus.Wishlist => "Wishlist",
        _ => "Backlog"
    };

    private static string NormalizeSort(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "rating" => "rating",
        "title" => "title",
        _ => "recent"
    };

    private static string? BuildCoverUrl(string? imageId) =>
        string.IsNullOrWhiteSpace(imageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{imageId}.jpg";
}

public sealed class UserReviewCard
{
    public int GameId { get; init; }
    public string GameName { get; init; } = string.Empty;
    public string? CoverUrl { get; init; }
    public int? ReleaseYear { get; init; }
    public string Status { get; init; } = GameLibraryStatus.Backlog;
    public int? Rating { get; init; }
    public string ReviewText { get; init; } = string.Empty;
    public bool ContainsSpoilers { get; init; }
    public DateTime UpdatedAt { get; init; }
}
