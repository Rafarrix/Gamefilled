using System.Text.Json.Serialization;

namespace Gamefilled.Infrastructure
{
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

        [JsonPropertyName("storyline")]
        public string? Storyline { get; set; }

        [JsonPropertyName("first_release_date")]
        public long? FirstReleaseDateUnix { get; set; }

        [JsonPropertyName("cover")]
        public IgdbCoverDto? Cover { get; set; }

        [JsonPropertyName("artworks")]
        public List<IgdbImageDto>? Artworks { get; set; }

        [JsonPropertyName("screenshots")]
        public List<IgdbImageDto>? Screenshots { get; set; }

        [JsonPropertyName("genres")]
        public List<IgdbNamedDto>? Genres { get; set; }

        [JsonPropertyName("platforms")]
        public List<IgdbNamedDto>? Platforms { get; set; }

        [JsonPropertyName("involved_companies")]
        public List<IgdbInvolvedCompanyDto>? InvolvedCompanies { get; set; }

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
    }

    public class IgdbNamedDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class IgdbInvolvedCompanyDto
    {
        [JsonPropertyName("developer")]
        public bool? Developer { get; set; }

        [JsonPropertyName("publisher")]
        public bool? Publisher { get; set; }

        [JsonPropertyName("company")]
        public IgdbCompanyDto? Company { get; set; }
    }

    public class IgdbCompanyDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
