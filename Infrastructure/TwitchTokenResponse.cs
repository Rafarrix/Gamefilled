using System.Text.Json.Serialization;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// DTO da resposta do endpoint OAuth do Twitch.
    /// Usado no fluxo client credentials para obter token de acesso à IGDB.
    /// </summary>
    public class TwitchTokenResponse
    {
        /// <summary>
        /// Token de acesso OAuth.
        /// </summary>
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Tempo de validade do token em segundos.
        /// </summary>
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        /// <summary>
        /// Tipo de token. Normalmente "bearer".
        /// </summary>
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }
}