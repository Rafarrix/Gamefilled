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

        var conditions = new List<string>
        {
            "cover != null",
            "slug != null",
            "name != null",
            "version_parent = null"
        };

        if (normalized.PlatformId.HasValue)
            conditions.Add($"platforms = {normalized.PlatformId.Value}");

        if (normalized.GenreId.HasValue)
            conditions.Add($"genres = {normalized.GenreId.Value}");

        if (normalized.ReleaseYear.HasValue)
        {
            var yearStart = new DateTimeOffset(
                normalized.ReleaseYear.Value,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero).ToUnixTimeSeconds();

            var nextYearStart = new DateTimeOffset(
                normalized.ReleaseYear.Value + 1,
                1,
                1,
                0,
                0,
                0,
                TimeSpan.Zero).ToUnixTimeSeconds();

            conditions.Add($"first_release_date >= {yearStart}");
            conditions.Add($"first_release_date < {nextYearStart}");
        }

        if (normalized.MinimumRating.HasValue)
        {
            var rating = normalized.MinimumRating.Value.ToString(
                "0.##",
                CultureInfo.InvariantCulture);

            conditions.Add("total_rating != null");
            conditions.Add($"total_rating >= {rating}");
        }

        if (normalized.IsReleased.HasValue)
        {
            conditions.Add("first_release_date != null");
            conditions.Add(normalized.IsReleased.Value
                ? $"first_release_date <= {now.ToUnixTimeSeconds()}"
                : $"first_release_date > {now.ToUnixTimeSeconds()}");
        }

        var sortLine = BuildSort(normalized, conditions, now);
        var whereLine = $"where {string.Join(" & ", conditions)};";
        var searchLine = string.IsNullOrWhiteSpace(normalized.Search)
            ? string.Empty
            : $"search \"{EscapeSearch(normalized.Search)}\";";

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
                // Fallback direto em /games. A integração PopScore será ligada
                // quando a biblioteca atual migrar integralmente para o serviço.
                conditions.Add("first_release_date != null");
                conditions.Add($"first_release_date >= {now.AddYears(-2).ToUnixTimeSeconds()}");
                conditions.Add($"first_release_date <= {now.ToUnixTimeSeconds()}");
                conditions.Add("follows != null");
                return $"sort follows {direction};";

            default:
                conditions.Add("total_rating_count != null");
                conditions.Add("total_rating_count >= 10");
                return $"sort total_rating_count {direction};";
        }
    }

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
