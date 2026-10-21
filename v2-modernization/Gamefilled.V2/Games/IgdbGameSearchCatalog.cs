using Gamefilled.Infrastructure;
using Gamefilled.V2.Library;
using Microsoft.Extensions.Caching.Memory;

namespace Gamefilled.V2.Games;

/// <summary>
/// Catalog lookup uses the existing SearchGamesAsync query and ordering
/// unchanged. A separate human review later evaluates search relevance.
/// </summary>
public interface IGameSearchCatalog
{
    Task<IReadOnlyList<GameDetailCard>> SearchAsync(
        string? query, CancellationToken cancellationToken = default);
}

public sealed class IgdbGameSearchCatalog(
    IgdbClient igdb,
    IMemoryCache cache) : IGameSearchCatalog
{
    public const int MaxLength = 80;
    public const int MaxResults = 12;

    public async Task<IReadOnlyList<GameDetailCard>> SearchAsync(
        string? query, CancellationToken cancellationToken = default)
    {
        var normalized = query?.Trim();
        if (normalized is null || normalized.Length is < 2 or > MaxLength)
            return [];

        // Data is public IGDB metadata, not the user's private library.
        var key = "igdb-search-v2:" + normalized.ToUpperInvariant();
        if (cache.TryGetValue<IReadOnlyList<GameDetailCard>>(key, out var cached)
            && cached is not null)
            return cached;

        // No modification to the existing search/category/filter query.
        var source = await igdb.SearchGamesAsync(normalized, MaxResults, cancellationToken);
        var results = GameSearchProjection.Project(source);
        cache.Set(key, results, TimeSpan.FromMinutes(3));
        return results;
    }
}

public static class GameSearchProjection
{
    public static IReadOnlyList<GameDetailCard> Project(IEnumerable<IgdbGameDto> games)
    {
        ArgumentNullException.ThrowIfNull(games);
        // Preserve IGDB order; never attempt category/ranking calibration
        // implicitly in a library picker.
        return games
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .Take(IgdbGameSearchCatalog.MaxResults)
            .Select(x => new GameDetailCard(
                x.Id,
                x.Name!.Trim(),
                IgdbLibraryGameCatalog.SafeCoverUrl(x.Cover?.ImageId),
                YearFromUnix(x.FirstReleaseDateUnix)))
            .ToArray();
    }

    private static int? YearFromUnix(long? unix)
    {
        if (unix is null or <= 0) return null;
        try { return DateTimeOffset.FromUnixTimeSeconds(unix.Value).Year; }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}
