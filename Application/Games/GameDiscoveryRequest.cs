namespace Gamefilled.Application.Games;

/// <summary>
/// Filtros, ordenação e paginação usados na descoberta de jogos.
/// A normalização garante que apenas valores seguros chegam ao query builder.
/// </summary>
public sealed class GameDiscoveryRequest
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 36;

    public string Sort { get; init; } = GameDiscoverySort.Popular;
    public string Direction { get; init; } = GameDiscoveryDirection.Descending;

    public string? Search { get; init; }
    public int? PlatformId { get; init; }
    public int? GenreId { get; init; }
    public int? ReleaseYear { get; init; }
    public double? MinimumRating { get; init; }

    /// <summary>
    /// null: todos; true: já lançados; false: futuros.
    /// </summary>
    public bool? IsReleased { get; init; }

    public GameDiscoveryRequest Normalize()
    {
        var normalizedSort = GameDiscoverySort.IsSupported(Sort)
            ? Sort.Trim().ToLowerInvariant()
            : GameDiscoverySort.Popular;

        var normalizedDirection = string.Equals(
            Direction,
            GameDiscoveryDirection.Ascending,
            StringComparison.OrdinalIgnoreCase)
                ? GameDiscoveryDirection.Ascending
                : GameDiscoveryDirection.Descending;

        var normalizedSearch = string.IsNullOrWhiteSpace(Search)
            ? null
            : Search.Trim();

        int? normalizedYear = ReleaseYear is >= 1950 and <= 2200
            ? ReleaseYear
            : null;

        double? normalizedRating = MinimumRating.HasValue
            ? Math.Clamp(MinimumRating.Value, 0d, 100d)
            : null;

        return new GameDiscoveryRequest
        {
            PageNumber = Math.Max(1, PageNumber),
            PageSize = Math.Clamp(PageSize, 12, 60),
            Sort = normalizedSort,
            Direction = normalizedDirection,
            Search = normalizedSearch,
            PlatformId = PlatformId is > 0 ? PlatformId : null,
            GenreId = GenreId is > 0 ? GenreId : null,
            ReleaseYear = normalizedYear,
            MinimumRating = normalizedRating,
            IsReleased = IsReleased
        };
    }
}

public static class GameDiscoverySort
{
    public const string Popular = "popular";
    public const string Trending = "trending";
    public const string TopRated = "top-rated";
    public const string ReleaseDate = "release-date";
    public const string Title = "title";

    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        Popular,
        Trending,
        TopRated,
        ReleaseDate,
        Title
    };

    public static bool IsSupported(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Supported.Contains(value);
}

public static class GameDiscoveryDirection
{
    public const string Ascending = "asc";
    public const string Descending = "desc";
}
