using System.Text.Json.Serialization;

namespace Gamefilled.Infrastructure;

public class IgdbGameDetailsDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("first_release_date")]
    public long? FirstReleaseDateUnix { get; set; }

    [JsonPropertyName("cover")]
    public IgdbCoverDto? Cover { get; set; }

    [JsonPropertyName("artworks")]
    public List<IgdbImageDto>? Artworks { get; set; }

    [JsonPropertyName("screenshots")]
    public List<IgdbImageDto>? Screenshots { get; set; }

    [JsonPropertyName("videos")]
    public List<IgdbVideoDto>? Videos { get; set; }

    [JsonPropertyName("genres")]
    public List<IgdbNamedDto>? Genres { get; set; }

    [JsonPropertyName("platforms")]
    public List<IgdbNamedDto>? Platforms { get; set; }

    [JsonPropertyName("involved_companies")]
    public List<IgdbInvolvedCompanyDto>? InvolvedCompanies { get; set; }

    [JsonPropertyName("release_dates")]
    public List<IgdbReleaseDateDto>? ReleaseDates { get; set; }

    [JsonPropertyName("websites")]
    public List<IgdbWebsiteDto>? Websites { get; set; }

    [JsonPropertyName("external_games")]
    public List<IgdbExternalGameDto>? ExternalGames { get; set; }

    [JsonPropertyName("parent_game")]
    public IgdbRelatedGameDto? ParentGame { get; set; }

    [JsonPropertyName("dlcs")]
    public List<IgdbRelatedGameDto>? Dlcs { get; set; }

    [JsonPropertyName("expansions")]
    public List<IgdbRelatedGameDto>? Expansions { get; set; }

    [JsonPropertyName("standalone_expansions")]
    public List<IgdbRelatedGameDto>? StandaloneExpansions { get; set; }

    [JsonPropertyName("expanded_games")]
    public List<IgdbRelatedGameDto>? ExpandedGames { get; set; }

    [JsonPropertyName("ports")]
    public List<IgdbRelatedGameDto>? Ports { get; set; }

    [JsonPropertyName("remakes")]
    public List<IgdbRelatedGameDto>? Remakes { get; set; }

    [JsonPropertyName("remasters")]
    public List<IgdbRelatedGameDto>? Remasters { get; set; }

    [JsonPropertyName("similar_games")]
    public List<IgdbRelatedGameDto>? SimilarGames { get; set; }

    [JsonPropertyName("aggregated_rating")]
    public double? AggregatedRating { get; set; }

    [JsonPropertyName("aggregated_rating_count")]
    public int? AggregatedRatingCount { get; set; }

    [JsonPropertyName("rating")]
    public double? Rating { get; set; }

    [JsonPropertyName("rating_count")]
    public int? RatingCount { get; set; }

    [JsonPropertyName("hypes")]
    public int? Hypes { get; set; }

    [JsonPropertyName("follows")]
    public int? Follows { get; set; }
}

public class IgdbImageDto
{
    [JsonPropertyName("image_id")]
    public string? ImageId { get; set; }

    [JsonPropertyName("width")]
    public int? Width { get; set; }

    [JsonPropertyName("height")]
    public int? Height { get; set; }
}

public class IgdbVideoDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("video_id")]
    public string? VideoId { get; set; }
}

public class IgdbNamedDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }
}

public class IgdbReleaseDateDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("date")]
    public long? DateUnix { get; set; }

    [JsonPropertyName("human")]
    public string? Human { get; set; }

    [JsonPropertyName("y")]
    public int? Year { get; set; }

    [JsonPropertyName("m")]
    public int? Month { get; set; }

    [JsonPropertyName("d")]
    public int? Day { get; set; }

    [JsonPropertyName("platform")]
    public IgdbNamedDto? Platform { get; set; }
}

public class IgdbWebsiteDto
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("trusted")]
    public bool Trusted { get; set; }

    [JsonPropertyName("type")]
    public IgdbWebsiteTypeDto? Type { get; set; }
}

public class IgdbWebsiteTypeDto
{
    [JsonPropertyName("type")]
    public string? Name { get; set; }
}

public class IgdbExternalGameDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("platform")]
    public IgdbNamedDto? Platform { get; set; }

    [JsonPropertyName("external_game_source")]
    public IgdbExternalGameSourceDto? Source { get; set; }
}

public class IgdbExternalGameSourceDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class IgdbRelatedGameDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("first_release_date")]
    public long? FirstReleaseDateUnix { get; set; }

    [JsonPropertyName("cover")]
    public IgdbCoverDto? Cover { get; set; }

    public string? CoverUrl =>
        string.IsNullOrWhiteSpace(Cover?.ImageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/t_cover_small/{Cover.ImageId}.jpg";

    public int? ReleaseYear
    {
        get
        {
            if (FirstReleaseDateUnix is null or <= 0)
                return null;

            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(FirstReleaseDateUnix.Value).Year;
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }
    }
}

public class IgdbInvolvedCompanyDto
{
    [JsonPropertyName("developer")]
    public bool Developer { get; set; }

    [JsonPropertyName("publisher")]
    public bool Publisher { get; set; }

    [JsonPropertyName("porting")]
    public bool Porting { get; set; }

    [JsonPropertyName("supporting")]
    public bool Supporting { get; set; }

    [JsonPropertyName("company")]
    public IgdbCompanyDto? Company { get; set; }
}

public class IgdbCompanyDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("slug")]
    public string? Slug { get; set; }

    [JsonPropertyName("logo")]
    public IgdbCompanyLogoDto? Logo { get; set; }
}

public class IgdbCompanyLogoDto
{
    [JsonPropertyName("image_id")]
    public string? ImageId { get; set; }
}
