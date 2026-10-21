using Gamefilled.Infrastructure;
using Gamefilled.V2.Library;
using Microsoft.Extensions.Caching.Memory;

namespace Gamefilled.V2.Games;

/// <summary>
/// Non-authoritative IGDB metadata. User status/reviews always live in Identity SQL.
/// </summary>
public sealed record GameDetailCard(int Id, string Name, string? CoverUrl, int? ReleaseYear);

public sealed record GameDetailInfo(
    int Id,
    string Name,
    string? Summary,
    string? CoverUrl,
    int? ReleaseYear,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<GameDetailCard> SimilarGames);

public interface IGameDetailCatalog
{
    Task<GameDetailInfo?> GetAsync(int gameId, CancellationToken ct = default);
}

/// <summary>
/// Uses the *existing* IGDB GetGameDetailsAsync query unchanged.
/// Does not adjust search, genre, category, ranking or discovery behavior.
/// </summary>
public sealed class IgdbGameDetailCatalog(
    IgdbClient igdb,
    IMemoryCache cache) : IGameDetailCatalog
{
    public async Task<GameDetailInfo?> GetAsync(int gameId, CancellationToken ct = default)
    {
        if (gameId <= 0)
            return null;

        if (cache.TryGetValue<GameDetailInfo>("igdb-game-detail-" + gameId, out var cached))
            return cached;

        var game = await igdb.GetGameDetailsAsync(gameId, ct);
        if (game is null || game.Id != gameId || string.IsNullOrWhiteSpace(game.Name))
            return null;

        var similar = (game.SimilarGames ?? [])
            .Where(x => x.Id > 0 && x.Id != gameId && !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .Take(8)
            .Select(x => new GameDetailCard(
                x.Id,
                x.Name!.Trim(),
                IgdbLibraryGameCatalog.SafeCoverUrl(x.Cover?.ImageId),
                x.ReleaseYear))
            .ToList();

        var view = new GameDetailInfo(
            game.Id,
            game.Name.Trim(),
            game.Summary,
            IgdbLibraryGameCatalog.SafeCoverUrl(game.Cover?.ImageId),
            SafeYear(game.FirstReleaseDateUnix),
            (game.Genres ?? []).Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList(),
            (game.Platforms ?? []).Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToList(),
            similar);

        cache.Set("igdb-game-detail-" + gameId, view, TimeSpan.FromMinutes(15));
        return view;
    }

    private static int? SafeYear(long? unix)
    {
        if (unix is null or <= 0) return null;
        try { return DateTimeOffset.FromUnixTimeSeconds(unix.Value).Year; }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}
