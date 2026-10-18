using System.Globalization;
using System.Text;

namespace Gamefilled.Application.Games;

/// <summary>
/// Constrói queries APICalypse de forma centralizada.
/// Mantém filtros e ordenação fora das Razor Pages e facilita testes futuros.
/// </summary>
public static class GameDiscoveryQueryBuilder
{
    private const string Fields =
        "fields id,name,slug,cover.image_id,first_release_date," +
        "total_rating,total_rating_count,hypes,follows," +
        "genres.id,genres.name,platforms.id,platforms.name;";

    public static GameDiscoveryQuery Build(
        GameDiscoveryRequest request,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalized = request.Normalize();
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var offset = (normalized.PageNumber - 1) * normalized.PageSize;
        var conditions = BuildBaseConditions(normalized, now);
        var sortLine = BuildSort(normalized, conditions, now);
        var whereLine = $"where {string.Join(" & ", conditions)};";
        var searchLine = BuildSearchLine(normalized.Search);

        var dataQuery = new StringBuilder()
            .Append(Fields)
            .Append(searchLine)
            .Append(whereLine)
            .Append(sortLine)
            .Append($"limit {normalized.PageSize};")
            .Append($"offset {offset};")
            .ToString();

        var countQuery = new StringBuilder()
            .Append(searchLine)
            .Append(whereLine)
            .ToString();

        return new GameDiscoveryQuery(
            normalized,
            dataQuery,
            countQuery,
            offset);
    }

    /// <summary>
    /// Constrói uma query para um conjunto ordenado de IDs obtidos pelo PopScore.
    /// A ordenação final é aplicada em memória pelo serviço para manter a ordem
    /// exata devolvida pelos popularity primitives.
    /// </summary>
    public static GameDiscoveryQuery BuildForGameIds(
        GameDiscoveryRequest request,
        IReadOnlyCollection<long> gameIds,
        DateTimeOffset? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(gameIds);

        var normalized = request.Normalize();
        var now = nowUtc ?? DateTimeOffset.UtcNow;
        var offset = (normalized.PageNumber - 1) * normalized.PageSize;
        var ids = gameIds
            .Where(id => id > 0)
            .Distinct()
            .Take(500)
            .ToArray();

        if (ids.Length == 0)
        {
            return new GameDiscoveryQuery(
                normalized,
                string.Empty,
                string.Empty,
                offset);
        }

        var conditions = BuildBaseConditions(normalized, now);
        conditions.Insert(0, $"id = ({string.Join(',', ids)})");

        var whereLine = $"where {string.Join(" & ", conditions)};";
        var searchLine = BuildSearchLine(normalized.Search);
        var dataQuery = new StringBuilder()
            .Append(Fields)
            .Append(searchLine)
            .Append(whereLine)
            .Append($"limit {ids.Length};")
            .ToString();

        return new GameDiscoveryQuery(
            normalized,
            dataQuery,
            searchLine + whereLine,
            offset);
    }

    private static List<string> BuildBaseConditions(
        GameDiscoveryRequest request,
        DateTimeOffset now)
    {
        var conditions = new List<string>
        {
            "cover != null",
            "slug != null",
            "name != null",
            "version_parent = null"
        };

        if (request.PlatformId.HasValue)
            conditions.Add($"platforms = {request.PlatformId.Value}");

        if (request.GenreId.HasValue)
            conditions.Add($"genres = {request.GenreId.Value}");

        if (request.ReleaseYear.HasValue)
        {
            var yearStart = new DateTimeOffset(
                request.ReleaseYear.Value,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero).ToUnixTimeSeconds();

            var nextYearStart = new DateTimeOffset(
                request.ReleaseYear.Value + 1,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero).ToUnixTimeSeconds();

            conditions.Add($"first_release_date >= {yearStart}");
            conditions.Add($"first_release_date < {nextYearStart}");
        }

        if (request.MinimumRating.HasValue)
        {
            var rating = request.MinimumRating.Value.ToString(
                "0.##",
                CultureInfo.InvariantCulture);

            conditions.Add("total_rating != null");
            conditions.Add($"total_rating >= {rating}");
        }

        if (request.IsReleased.HasValue)
        {
            conditions.Add("first_release_date != null");
            conditions.Add(request.IsReleased.Value
                ? $"first_release_date <= {now.ToUnixTimeSeconds()}"
                : $"first_release_date > {now.ToUnixTimeSeconds()}");
        }

        return conditions;
    }

    private static string BuildSort(
        GameDiscoveryRequest request,
        ICollection<string> conditions,
        DateTimeOffset now)
    {
        var direction = request.Direction;

        switch (request.Sort)
        {
            case GameDiscoverySort.Title:
                return $"sort name {direction};";

            case GameDiscoverySort.ReleaseDate:
                conditions.Add("first_release_date != null");
                return $"sort first_release_date {direction};";

            case GameDiscoverySort.TopRated:
                conditions.Add("total_rating != null");
                conditions.Add("total_rating_count != null");
                conditions.Add("total_rating_count >= 20");
                return $"sort total_rating {direction};";

            case GameDiscoverySort.Trending:
                // Fallback usado apenas se o PopScore estiver indisponível.
                // É intencionalmente amplo para nunca deixar a página vazia.
                conditions.Add("first_release_date != null");
                conditions.Add($"first_release_date >= {now.AddYears(-5).ToUnixTimeSeconds()}");
                conditions.Add($"first_release_date <= {now.ToUnixTimeSeconds()}");
                conditions.Add("total_rating_count != null");
                return $"sort total_rating_count {direction};";

            default:
                conditions.Add("total_rating_count != null");
                conditions.Add("total_rating_count >= 10");
                return $"sort total_rating_count {direction};";
        }
    }

    private static string BuildSearchLine(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? string.Empty
            : $"search \"{EscapeSearch(search)}\";";

    private static string EscapeSearch(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
}

public sealed record GameDiscoveryQuery(
    GameDiscoveryRequest Request,
    string DataQuery,
    string CountQuery,
    int Offset);
