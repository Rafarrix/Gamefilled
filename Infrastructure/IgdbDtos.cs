using System.Text.Json.Serialization;

namespace Gamefilled.Infrastructure
{
    public class IgdbGameDto
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
    }

    public class IgdbCoverDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("image_id")]
        public string? ImageId { get; set; }
    }
}
