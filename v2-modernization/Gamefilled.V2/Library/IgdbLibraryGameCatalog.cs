using Gamefilled.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace Gamefilled.V2.Library;

/// <summary>
/// Display-only catalog data. Game ownership, ratings and reviews always come
/// from the local Identity SQL database and are never inferred from IGDB.
/// </summary>
public sealed record LibraryGameMetadata(int GameId, string Name, string? CoverUrl, int? ReleaseYear);

public interface ILibraryGameCatalog
{
    Task<IReadOnlyDictionary<int, LibraryGameMetadata>> GetByIdsAsync(
        IEnumerable<int> gameIds, CancellationToken cancellationToken = default);
}

/// <summary>
/// Intentionally calls the existing IgdbClient.GetGamesByIdsAsync unchanged.
/// It does NOT call search, discovery, genre/categorization or special queries.
/// </summary>
public sealed class IgdbLibraryGameCatalog(
    IgdbClient igdb,
    IMemoryCache cache,
    ILogger<IgdbLibraryGameCatalog> logger) : ILibraryGameCatalog
{
    private const int MaxBatch = 24;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(30);

    public async Task<IReadOnlyDictionary<int, LibraryGameMetadata>> GetByIdsAsync(
        IEnumerable<int> gameIds, CancellationToken cancellationToken = default)
    {
        var ids = gameIds.Where(id => id > 0).Distinct().Take(MaxBatch).ToArray();
        var result = new Dictionary<int, LibraryGameMetadata>();
        var uncached = new List<int>();

        foreach (var id in ids)
        {
            if (cache.TryGetValue<LibraryGameMetadata>("igdb-library-" + id, out var cached)
                && cached is not null)
                result[id] = cached;
            else
                uncached.Add(id);
        }

        if (uncached.Count == 0)
            return result;

        try
        {
            // Existing IGDB APICalypse query remains EXACTLY as it was.
            var games = await igdb.GetGamesByIdsAsync(uncached, cancellationToken);
            var allowed = uncached.ToHashSet();
            foreach (var game in games)
            {
                if (!allowed.Contains(game.Id) || string.IsNullOrWhiteSpace(game.Name))
                    continue;

                var year = YearFromUnix(game.FirstReleaseDateUnix);
                var metadata = new LibraryGameMetadata(
                    game.Id, game.Name.Trim(), SafeCoverUrl(game.Cover?.ImageId), year);
                result[game.Id] = metadata;
                cache.Set("igdb-library-" + game.Id, metadata, CacheLifetime);
            }
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("IGDB library metadata temporarily unavailable (HTTP).");
        }
        catch (InvalidOperationException)
        {
            // Includes deliberately unconfigured optional IGDB credentials in CI.
            logger.LogWarning("IGDB library metadata temporarily unavailable.");
        }

        return result;
    }

    private static int? YearFromUnix(long? unix)
    {
        if (unix is null or <= 0) return null;
        try { return DateTimeOffset.FromUnixTimeSeconds(unix.Value).Year; }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static string? SafeCoverUrl(string? imageId)
    {
        if (string.IsNullOrWhiteSpace(imageId)
            || !Regex.IsMatch(imageId, @"\A[a-zA-Z0-9_-]{1,60}\z",
                RegexOptions.CultureInvariant))
            return null;
        return $"https://images.igdb.com/igdb/image/upload/t_cover_big/{imageId}.jpg";
    }
}
