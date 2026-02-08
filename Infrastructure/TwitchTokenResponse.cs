using System.Text.Json.Serialization;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// ✅ Resposta do endpoint OAuth do Twitch (client credentials)
    /// </summary>
    public class TwitchTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; } // em segundos

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }
}
