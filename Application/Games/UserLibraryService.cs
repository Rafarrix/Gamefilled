using Gamefilled.Data;
using Gamefilled.Infrastructure;
using Gamefilled.Models;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Application.Games;

public sealed record UserLibraryOwner(
    int Id,
    string Username,
    string DisplayName,
    string? AvatarUrl,
    string? Bio);

public sealed record UserLibraryCard(
    int GameId,
    string Name,
    string? CoverUrl,
    int? ReleaseYear,
    string Status,
    int? Rating,
    bool HasReview,
    DateTime UpdatedAt);

public sealed record UserLibraryResult(
    UserLibraryOwner Owner,
    IReadOnlyList<UserLibraryCard> Games,
    IReadOnlyDictionary<string, int> Counts,
    int TotalItems,
    int TotalPages,
    int PageNumber);

public sealed class UserLibraryService
{
    private const int PageSize = 24;

    private readonly AppDbContext _db;
    private readonly IgdbClient _igdb;

    public UserLibraryService(AppDbContext db, IgdbClient igdb)
    {
        _db = db;
        _igdb = igdb;
    }

    public async Task<UserLibraryResult?> GetAsync(
        string username,
        string status,
        string? search,
        string? sort,
        int page,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Username == username, cancellationToken);

        if (user is null)
            return null;

        var entries = await _db.UserGameEntries
            .AsNoTracking()
            .Where(entry => entry.UserId == user.Id)
            .ToListAsync(cancellationToken);

        var counts = GameLibraryStatus.All.ToDictionary(
            libraryStatus => libraryStatus,
            libraryStatus => entries.Count(entry =>
                string.Equals(entry.Status, libraryStatus, StringComparison.OrdinalIgnoreCase)),
            StringComparer.OrdinalIgnoreCase);

        var selectedEntries = entries
            .Where(entry => string.Equals(entry.Status, status, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var games = await _igdb.GetGamesByIdsAsync(
            selectedEntries.Select(entry => entry.GameId),
            cancellationToken);
        var gamesById = games.ToDictionary(game => game.Id);

        var cards = selectedEntries
            .Select(entry =>
            {
                gamesById.TryGetValue(entry.GameId, out var game);
                var releaseYear = game?.FirstReleaseDateUnix is long unix
                    ? DateTimeOffset.FromUnixTimeSeconds(unix).Year
                    : (int?)null;

                return new UserLibraryCard(
                    entry.GameId,
                    game?.Name ?? $"Game {entry.GameId}",
                    BuildCoverUrl(game?.Cover?.ImageId),
                    releaseYear,
                    entry.Status,
                    entry.Rating,
                    !string.IsNullOrWhiteSpace(entry.ReviewText),
                    entry.UpdatedAt);
            })
            .ToList();

        search = search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            cards = cards
                .Where(card => card.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        cards = NormalizeSort(sort) switch
        {
            "title" => cards.OrderBy(card => card.Name).ToList(),
            "rating" => cards
                .OrderByDescending(card => card.Rating.HasValue)
                .ThenByDescending(card => card.Rating)
                .ThenBy(card => card.Name)
                .ToList(),
            "release" => cards
                .OrderByDescending(card => card.ReleaseYear.HasValue)
                .ThenByDescending(card => card.ReleaseYear)
                .ThenBy(card => card.Name)
                .ToList(),
            _ => cards.OrderByDescending(card => card.UpdatedAt).ToList()
        };

        var totalItems = cards.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        var pageGames = cards
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToList();

        return new UserLibraryResult(
            new UserLibraryOwner(
                user.Id,
                user.Username ?? username,
                string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username ?? username : user.DisplayName,
                user.AvatarUrl,
                user.Bio),
            pageGames,
            counts,
            totalItems,
            totalPages,
            page);
    }

    public static string NormalizeSort(string? sort) => sort?.Trim().ToLowerInvariant() switch
    {
        "title" => "title",
        "rating" => "rating",
        "release" => "release",
        _ => "recent"
    };

    private static string? BuildCoverUrl(string? imageId) =>
        string.IsNullOrWhiteSpace(imageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{imageId}.jpg";
}
