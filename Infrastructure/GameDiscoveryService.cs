using System.Text.Json.Serialization;
using Gamefilled.Application.Games;
using Microsoft.Extensions.Caching.Memory;

namespace Gamefilled.Infrastructure;

/// <summary>
/// Implementação central da descoberta de jogos.
/// Constrói queries através de GameDiscoveryQueryBuilder e converte a resposta
/// da IGDB em modelos próprios da aplicação.
/// </summary>
public sealed class GameDiscoveryService : IGameDiscoveryService
{
    private const string PlatformsCacheKey = "igdb:metadata:platforms:v1";
    private const string GenresCacheKey = "igdb:metadata:genres:v1";

    private static readonly TimeSpan MetadataCacheDuration = TimeSpan.FromHours(12);

    private readonly IgdbApiClient _apiClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GameDiscoveryService> _logger;

    public GameDiscoveryService(
        IgdbApiClient apiClient,
        IMemoryCache cache,
        ILogger<GameDiscoveryService> logger)
    {
        _apiClient = apiClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GameDiscoveryResult> SearchAsync(
        GameDiscoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = GameDiscoveryQueryBuilder.Build(request);

        var rows = await _apiClient.QueryAsync<List<IgdbDiscoveryGameDto>>(
            "games",
            query.DataQuery,
            cancellationToken);

        var totalCount = await TryGetCountAsync(query, rows.Count, cancellationToken);
        var totalPages = totalCount <= 0
            ? 0
            : (int)Math.Ceiling(totalCount / (double)query.Request.PageSize);

        return new GameDiscoveryResult
        {
            Games = rows
                .Where(IsUsableGame)
                .Select(MapGame)
                .ToList(),
            TotalCount = totalCount,
            TotalPages = totalPages,
            PageNumber = query.Request.PageNumber,
            PageSize = query.Request.PageSize,
            DiagnosticQuery = query.DataQuery
        };
    }

    public async Task<IReadOnlyList<GameFilterOption>> GetPlatformsAsync(
        CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetOrCreateAsync(
            PlatformsCacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = MetadataCacheDuration;

                var rows = await _apiClient.QueryAsync<List<IgdbNamedEntityDto>>(
                    "platforms",
                    "fields id,name; where name != null; sort name asc; limit 500;",
                    cancellationToken);

                return BuildOptions(rows);
            });

        return cached ?? [];
    }

    public async Task<IReadOnlyList<GameFilterOption>> GetGenresAsync(
        CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetOrCreateAsync(
            GenresCacheKey,
            async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = MetadataCacheDuration;

                var rows = await _apiClient.QueryAsync<List<IgdbNamedEntityDto>>(
                    "genres",
                    "fields id,name; where name != null; sort name asc; limit 100;",
                    cancellationToken);

                return BuildOptions(rows);
            });

        return cached ?? [];
    }

    private async Task<int> TryGetCountAsync(
        GameDiscoveryQuery query,
        int returnedCount,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _apiClient.CountAsync(
                "games",
                query.CountQuery,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or InvalidOperationException)
        {
            // O endpoint /count pode não aceitar todas as combinações de search.
            // A página continua funcional com uma estimativa conservadora.
            _logger.LogWarning(
                exception,
                "Unable to count IGDB discovery results. Falling back to an estimate.");

            return query.Offset + returnedCount;
        }
    }

    private static IReadOnlyList<GameFilterOption> BuildOptions(
        IEnumerable<IgdbNamedEntityDto> rows) =>
        rows
            .Where(row => row.Id > 0 && !string.IsNullOrWhiteSpace(row.Name))
            .Select(row => new GameFilterOption(row.Id, row.Name!.Trim()))
            .DistinctBy(option => option.Id)
            .OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static bool IsUsableGame(IgdbDiscoveryGameDto game) =>
        game.Id > 0 &&
        !string.IsNullOrWhiteSpace(game.Name) &&
        !string.IsNullOrWhiteSpace(game.Slug);

    private static GameDiscoveryCard MapGame(IgdbDiscoveryGameDto game) =>
        new()
        {
            Id = game.Id,
            Name = game.Name!.Trim(),
            Slug = game.Slug!.Trim(),
            CoverImageId = game.Cover?.ImageId,
            FirstReleaseDate = game.FirstReleaseDate,
            TotalRating = game.TotalRating,
            TotalRatingCount = game.TotalRatingCount,
            Genres = BuildOptions(game.Genres ?? []),
            Platforms = BuildOptions(game.Platforms ?? [])
        };

    private sealed class IgdbDiscoveryGameDto
    {
        [JsonPropertyName("id")]
        public long Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("slug")]
        public string? Slug { get; init; }

        [JsonPropertyName("cover")]
        public IgdbCoverDto? Cover { get; init; }

        [JsonPropertyName("first_release_date")]
        public long? FirstReleaseDate { get; init; }

        [JsonPropertyName("total_rating")]
        public double? TotalRating { get; init; }

        [JsonPropertyName("total_rating_count")]
        public int? TotalRatingCount { get; init; }

        [JsonPropertyName("genres")]
        public List<IgdbNamedEntityDto>? Genres { get; init; }

        [JsonPropertyName("platforms")]
        public List<IgdbNamedEntityDto>? Platforms { get; init; }
    }

    private sealed class IgdbNamedEntityDto
    {
        [JsonPropertyName("id")]
        public int Id { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }
}
