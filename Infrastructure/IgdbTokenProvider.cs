using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// ✅ Responsável por obter e guardar (em memória) o token do Twitch.
    /// - Pede um token quando for preciso
    /// - Reusa até expirar
    /// </summary>
    public class IgdbTokenProvider
    {
        private readonly HttpClient _http;
        private readonly IgdbOptions _options;

        private string? _token;
        private DateTimeOffset _tokenExpiresAtUtc;

        public IgdbTokenProvider(HttpClient http, IOptions<IgdbOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        /// <summary>
        /// Devolve sempre um token válido (renova se expirou).
        /// </summary>
        public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
        {
            // ✅ Se ainda é válido, reutiliza
            if (!string.IsNullOrWhiteSpace(_token) && DateTimeOffset.UtcNow < _tokenExpiresAtUtc)
                return _token!;

            // ✅ Validar config
            if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
                throw new InvalidOperationException("IGDB ClientId/ClientSecret em falta no appsettings.json.");

            // Twitch OAuth client credentials:
            // POST https://id.twitch.tv/oauth2/token?client_id=...&client_secret=...&grant_type=client_credentials :contentReference[oaicite:4]{index=4}
            var url =
                $"https://id.twitch.tv/oauth2/token" +
                $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
                $"&client_secret={Uri.EscapeDataString(_options.ClientSecret)}" +
                $"&grant_type=client_credentials";

            // O Twitch aceita POST (sem body) com querystring (como nas docs)
            using var resp = await _http.PostAsync(url, content: null, ct);
            resp.EnsureSuccessStatusCode();

            var data = await resp.Content.ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken: ct);
            if (data == null || string.IsNullOrWhiteSpace(data.AccessToken))
                throw new InvalidOperationException("Resposta de token inválida do Twitch.");

            _token = data.AccessToken;

            // ✅ expiração (pomos uma folga de 60s para não falhar no limite)
            _tokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, data.ExpiresIn - 60));

            return _token!;
        }
    }
}