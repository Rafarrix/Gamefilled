using System.Globalization;

namespace Gamefilled.Application.Games;

public sealed class GameDiscoveryResult
{
    public IReadOnlyList<GameDiscoveryCard> Games { get; init; } = [];
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
    public string? DiagnosticQuery { get; init; }
}

public sealed class GameDiscoveryCard
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? CoverImageId { get; init; }
    public long? FirstReleaseDate { get; init; }
    public double? TotalRating { get; init; }
    public int? TotalRatingCount { get; init; }

    public IReadOnlyList<GameFilterOption> Genres { get; init; } = [];
    public IReadOnlyList<GameFilterOption> Platforms { get; init; } = [];

    public int? Year
    {
        get
        {
            if (FirstReleaseDate is null or <= 0)
                return null;

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(FirstReleaseDate.Value).Year;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }

    public string? CoverUrl =>
        string.IsNullOrWhiteSpace(CoverImageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{CoverImageId}.jpg";

    public string? ReleaseDateText
    {
        get
        {
            if (FirstReleaseDate is null or <= 0)
                return null;

            try
            {
                var date = DateTimeOffset.FromUnixTimeSeconds(FirstReleaseDate.Value);
                return date.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("pt-PT"));
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }

    public string? RatingText =>
        TotalRating.HasValue
            ? $"★ {TotalRating.Value:0.#}"
            : null;
}

public sealed record GameFilterOption(int Id, string Name);
