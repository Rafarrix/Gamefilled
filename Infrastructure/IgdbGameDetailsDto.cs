using System.Text.Json.Serialization;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// DTO completo de detalhe de jogo.
    /// Usado na página /games/{id}.
    /// </summary>
    public class IgdbGameDetailsDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        /// <summary>
        /// Resumo principal do jogo.
        /// </summary>
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        /// <summary>
        /// Storyline / descrição narrativa mais longa.
        /// </summary>
        [JsonPropertyName("storyline")]
        public string? Storyline { get; set; }

        /// <summary>
        /// Data de lançamento em UNIX timestamp.
        /// </summary>
        [JsonPropertyName("first_release_date")]
        public long? FirstReleaseDateUnix { get; set; }

        /// <summary>
        /// Cover principal.
        /// </summary>
        [JsonPropertyName("cover")]
        public IgdbCoverDto? Cover { get; set; }

        /// <summary>
        /// Artworks/promotional images.
        /// </summary>
        [JsonPropertyName("artworks")]
        public List<IgdbImageDto>? Artworks { get; set; }

        /// <summary>
        /// Screenshots do jogo.
        /// </summary>
        [JsonPropertyName("screenshots")]
        public List<IgdbImageDto>? Screenshots { get; set; }

        /// <summary>
        /// Géneros do jogo.
        /// </summary>
        [JsonPropertyName("genres")]
        public List<IgdbNamedDto>? Genres { get; set; }

        /// <summary>
        /// Plataformas em que o jogo existe.
        /// </summary>
        [JsonPropertyName("platforms")]
        public List<IgdbNamedDto>? Platforms { get; set; }

        /// <summary>
        /// Empresas envolvidas (publisher/developer).
        /// </summary>
        [JsonPropertyName("involved_companies")]
        public List<IgdbInvolvedCompanyDto>? InvolvedCompanies { get; set; }

        /// <summary>
        /// Rating agregado.
        /// </summary>
        [JsonPropertyName("aggregated_rating")]
        public double? AggregatedRating { get; set; }

        /// <summary>
        /// Número de ratings agregados.
        /// </summary>
        [JsonPropertyName("aggregated_rating_count")]
        public int? AggregatedRatingCount { get; set; }

        /// <summary>
        /// Rating normal.
        /// </summary>
        [JsonPropertyName("rating")]
        public double? Rating { get; set; }

        /// <summary>
        /// Número de ratings normais.
        /// </summary>
        [JsonPropertyName("rating_count")]
        public int? RatingCount { get; set; }

        /// <summary>
        /// Número de hypes.
        /// </summary>
        [JsonPropertyName("hypes")]
        public int? Hypes { get; set; }

        /// <summary>
        /// Número de follows.
        /// </summary>
        [JsonPropertyName("follows")]
        public int? Follows { get; set; }
    }

    /// <summary>
    /// DTO simples para imagens com image_id.
    /// </summary>
    public class IgdbImageDto
    {
        [JsonPropertyName("image_id")]
        public string? ImageId { get; set; }
    }

    /// <summary>
    /// DTO genérico para objetos com propriedade "name".
    /// Ex.: genres, platforms.
    /// </summary>
    public class IgdbNamedDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    /// <summary>
    /// DTO de empresa envolvida no jogo.
    /// </summary>
    public class IgdbInvolvedCompanyDto
    {
        /// <summary>
        /// Indica se a empresa foi developer.
        /// </summary>
        [JsonPropertyName("developer")]
        public bool? Developer { get; set; }

        /// <summary>
        /// Indica se a empresa foi publisher.
        /// </summary>
        [JsonPropertyName("publisher")]
        public bool? Publisher { get; set; }

        /// <summary>
        /// Dados da empresa.
        /// </summary>
        [JsonPropertyName("company")]
        public IgdbCompanyDto? Company { get; set; }
    }

    /// <summary>
    /// DTO de empresa da IGDB.
    /// </summary>
    public class IgdbCompanyDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}