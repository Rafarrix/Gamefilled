using System.Text.Json.Serialization;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// DTO simplificado de jogo vindo da IGDB.
    /// Usado em listas, pesquisas e grids.
    /// </summary>
    public class IgdbGameDto
    {
        /// <summary>
        /// ID único do jogo na IGDB.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Nome do jogo.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Slug do jogo na IGDB.
        /// </summary>
        [JsonPropertyName("slug")]
        public string? Slug { get; set; }

        /// <summary>
        /// Data de lançamento em formato UNIX timestamp.
        /// </summary>
        [JsonPropertyName("first_release_date")]
        public long? FirstReleaseDateUnix { get; set; }

        /// <summary>
        /// Dados da cover do jogo.
        /// </summary>
        [JsonPropertyName("cover")]
        public IgdbCoverDto? Cover { get; set; }
    }

    /// <summary>
    /// DTO da cover/imagem principal de um jogo.
    /// </summary>
    public class IgdbCoverDto
    {
        /// <summary>
        /// ID da cover na IGDB.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// ImageId usado para construir a URL da imagem.
        /// </summary>
        [JsonPropertyName("image_id")]
        public string? ImageId { get; set; }
    }
}