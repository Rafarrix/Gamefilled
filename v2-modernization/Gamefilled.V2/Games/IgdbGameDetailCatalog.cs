using Gamefilled.Infrastructure;
using Gamefilled.V2.Library;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace Gamefilled.V2.Games;

/// <summary>Display-only IGDB results; owned entries always live in Identity SQL.</summary>
public sealed record GameDetailCard(int Id, string Name, string? CoverUrl, int? ReleaseYear);
public sealed record GameDetailMedia(string Url, string Name);
public sealed record GameDetailVideo(string WatchUrl, string ThumbnailUrl, string Name);
public sealed record GameDetailRelease(int? Year, string Label, string? Platform);
public sealed record GameDetailUniverseSection(string Title, IReadOnlyList<GameDetailCard> Games);
public sealed record GameDetailCompany(string Name, string Role);

public sealed record GameDetailInfo(
    int Id,
    string Name,
    string? Summary,
    string? CoverUrl,
    int? ReleaseYear,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<GameDetailCard> SimilarGames)
{
    public IReadOnlyList<GameDetailMedia> Screenshots { get; init; } = [];
    public IReadOnlyList<GameDetailVideo> Videos { get; init; } = [];
    public IReadOnlyList<GameDetailRelease> ReleaseTimeline { get; init; } = [];
    public IReadOnlyList<GameDetailUniverseSection> Universe { get; init; } = [];
    public IReadOnlyList<GameDetailCompany> Companies { get; init; } = [];
    public double? CommunityRating { get; init; }
    public int? CommunityRatingCount { get; init; }
}

public interface IGameDetailCatalog
{
    Task<GameDetailInfo?> GetAsync(int gameId, CancellationToken ct = default);
}

/// <summary>
/// Uses the existing IGDB GetGameDetailsAsync APICalypse query without changing
/// search, category, discovery, filter or sorting criteria.
/// </summary>
public sealed class IgdbGameDetailCatalog(
    IgdbClient igdb,
    IMemoryCache cache) : IGameDetailCatalog
{
    public async Task<GameDetailInfo?> GetAsync(int gameId, CancellationToken ct = default)
    {
        if (gameId <= 0) return null;

        if (cache.TryGetValue<GameDetailInfo>("igdb-game-detail-" + gameId, out var cached))
            return cached;

        var source = await igdb.GetGameDetailsAsync(gameId, ct);
        if (source is null || source.Id != gameId)
            return null;

        var result = GameDetailProjection.Project(source);
        if (result is not null)
            cache.Set("igdb-game-detail-" + gameId, result, TimeSpan.FromMinutes(15));

        return result;
    }
}

/// <summary>
/// Pure projector: safely turns existing IGDB detail DTOs into bounded, encoded
/// Razor-friendly data. No extra HTTP requests and no user identity access.
/// </summary>
public static class GameDetailProjection
{
    private static readonly Regex SafeImageId = new(
        @"\A[a-zA-Z0-9_-]{1,60}\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex YoutubeVideoId = new(
        @"\A[a-zA-Z0-9_-]{11}\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static GameDetailInfo? Project(IgdbGameDetailsDto source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Id <= 0 || string.IsNullOrWhiteSpace(source.Name))
            return null;

        var result = new GameDetailInfo(
            source.Id,
            source.Name.Trim(),
            source.Summary,
            IgdbLibraryGameCatalog.SafeCoverUrl(source.Cover?.ImageId),
            YearFromUnix(source.FirstReleaseDateUnix),
            Names(source.Genres, 12),
            Names(source.Platforms, 16),
            Related(source.SimilarGames, source.Id, 8))
        {
            Screenshots = BuildMedia(source),
            Videos = BuildVideos(source.Videos),
            ReleaseTimeline = BuildReleases(source.ReleaseDates),
            Universe = BuildUniverse(source),
            Companies = BuildCompanies(source.InvolvedCompanies),
            CommunityRating = ValidRating(source.AggregatedRating ?? source.Rating),
            CommunityRatingCount = (source.AggregatedRatingCount ?? source.RatingCount) is > 0
                ? source.AggregatedRatingCount ?? source.RatingCount
                : null
        };
        return result;
    }

    private static IReadOnlyList<string> Names(IEnumerable<IgdbNamedDto>? source, int limit) =>
        (source ?? [])
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => x.Name!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToArray();

    private static IReadOnlyList<GameDetailCard> Related(
        IEnumerable<IgdbRelatedGameDto>? source, int currentId, int limit) =>
        (source ?? [])
            .Where(x => x.Id > 0 && x.Id != currentId && !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .Take(limit)
            .Select(x => new GameDetailCard(
                x.Id, x.Name!.Trim(),
                IgdbLibraryGameCatalog.SafeCoverUrl(x.Cover?.ImageId),
                x.ReleaseYear))
            .ToArray();

    private static IReadOnlyList<GameDetailMedia> BuildMedia(IgdbGameDetailsDto game)
    {
        var input = (game.Screenshots ?? []).Concat(game.Artworks ?? []);
        return input
            .Where(x => x.ImageId is not null && SafeImageId.IsMatch(x.ImageId))
            .GroupBy(x => x.ImageId!, StringComparer.Ordinal)
            .Select(x => x.Key)
            .Take(8)
            .Select(x => new GameDetailMedia(
                $"https://images.igdb.com/igdb/image/upload/t_screenshot_big/{x}.jpg",
                $"{game.Name} screenshot"))
            .ToArray();
    }

    private static IReadOnlyList<GameDetailVideo> BuildVideos(IEnumerable<IgdbVideoDto>? videos) =>
        (videos ?? [])
            .Where(x => x.VideoId is not null && YoutubeVideoId.IsMatch(x.VideoId))
            .GroupBy(x => x.VideoId!, StringComparer.Ordinal)
            .Select(x => x.First())
            .Take(3)
            .Select(x => new GameDetailVideo(
                $"https://www.youtube.com/watch?v={x.VideoId}",
                $"https://i.ytimg.com/vi/{x.VideoId}/hqdefault.jpg",
                string.IsNullOrWhiteSpace(x.Name) ? "Watch trailer" : x.Name.Trim()))
            .ToArray();

    private static IReadOnlyList<GameDetailRelease> BuildReleases(IEnumerable<IgdbReleaseDateDto>? dates) =>
        (dates ?? [])
            .Where(x => x.DateUnix is > 0 || x.Year is > 0 || !string.IsNullOrWhiteSpace(x.Human))
            .Select(x => new GameDetailRelease(
                x.Year is > 0 and < 3000 ? x.Year : YearFromUnix(x.DateUnix),
                string.IsNullOrWhiteSpace(x.Human)
                    ? (x.Year is > 0 ? x.Year.ToString() : "Release date unavailable")
                    : x.Human.Trim(),
                x.Platform?.Name))
            .Distinct()
            .OrderBy(x => x.Year ?? int.MaxValue)
            .ThenBy(x => x.Platform, StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToArray();

    private static IReadOnlyList<GameDetailUniverseSection> BuildUniverse(IgdbGameDetailsDto game)
    {
        var output = new List<GameDetailUniverseSection>();

        void Include(string label, IEnumerable<IgdbRelatedGameDto>? source)
        {
            var cards = Related(source, game.Id, 12)
                .OrderBy(x => x.ReleaseYear ?? int.MaxValue)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (cards.Length > 0)
                output.Add(new GameDetailUniverseSection(label, cards));
        }

        if (game.ParentGame is not null)
            Include("Parent game", [game.ParentGame]);

        Include("DLC", game.Dlcs);
        Include("Expansions", game.Expansions);
        Include("Standalone expansions", game.StandaloneExpansions);
        Include("Expanded games", game.ExpandedGames);
        Include("Ports", game.Ports);
        Include("Remakes", game.Remakes);
        Include("Remasters", game.Remasters);
        return output;
    }

    private static IReadOnlyList<GameDetailCompany> BuildCompanies(
        IEnumerable<IgdbInvolvedCompanyDto>? companies) =>
        (companies ?? [])
            .Where(x => x.Company is { Id: > 0 } && !string.IsNullOrWhiteSpace(x.Company.Name))
            .SelectMany(x =>
            {
                var roles = new List<GameDetailCompany>();
                if (x.Developer) roles.Add(new GameDetailCompany(x.Company!.Name!.Trim(), "Developer"));
                if (x.Publisher) roles.Add(new GameDetailCompany(x.Company!.Name!.Trim(), "Publisher"));
                return roles;
            })
            .Distinct()
            .Take(12)
            .ToArray();

    private static int? YearFromUnix(long? unix)
    {
        if (unix is null or <= 0) return null;
        try { return DateTimeOffset.FromUnixTimeSeconds(unix.Value).Year; }
        catch (ArgumentOutOfRangeException) { return null; }
    }

    private static double? ValidRating(double? raw) =>
        raw is >= 0 and <= 100 && !double.IsNaN(raw.Value) && !double.IsInfinity(raw.Value)
            ? raw
            : null;
}
