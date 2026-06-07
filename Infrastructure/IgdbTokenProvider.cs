using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// Serviço responsável por obter e guardar em memória o token OAuth do Twitch,
    /// necessário para aceder à API IGDB.
    ///
    /// O que este ficheiro faz:
    /// - pede token novo quando necessário
    /// - reutiliza o token enquanto for válido
    /// - aplica margem de segurança antes da expiração
    ///
    /// Importância:
    /// Evita pedir um token novo em todos os requests, tornando o sistema
    /// mais eficiente e organizado.
    /// </summary>
    public class IgdbTokenProvider
    {
        /// <summary>
        /// HttpClient usado para chamar o endpoint OAuth do Twitch.
        /// </summary>
        private readonly HttpClient _http;

        /// <summary>
        /// Configuração da IGDB (ClientId e ClientSecret).
        /// </summary>
        private readonly IgdbOptions _options;

        /// <summary>
        /// Token atualmente guardado em memória.
        /// </summary>
        private string? _token;

        /// <summary>
        /// Data/hora em que o token deixa de ser considerado válido.
        /// </summary>
        private DateTimeOffset _tokenExpiresAtUtc;

        public IgdbTokenProvider(HttpClient http, IOptions<IgdbOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        /// <summary>
        /// Devolve sempre um token válido.
        /// Se o token em memória ainda for válido, reutiliza-o.
        /// Caso contrário, pede um novo token ao Twitch.
        /// </summary>
        public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
        {
            // Se já existir token e ainda for válido, reutiliza.
            if (!string.IsNullOrWhiteSpace(_token) && DateTimeOffset.UtcNow < _tokenExpiresAtUtc)
                return _token!;

            // Validação da configuração.
            if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
                throw new InvalidOperationException("IGDB ClientId/ClientSecret em falta no appsettings.json.");

            // Endpoint OAuth do Twitch.
            var url = "https://id.twitch.tv/oauth2/token";

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["grant_type"] = "client_credentials"
            });

            // Pedido de token ao Twitch.
            using var resp = await _http.PostAsync(url, content, ct);

            // Lança exceção automática se houver erro HTTP.
            resp.EnsureSuccessStatusCode();

            // Lê a resposta JSON para o DTO correspondente.
            var data = await resp.Content.ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken: ct);

            // Garante que veio token válido.
            if (data == null || string.IsNullOrWhiteSpace(data.AccessToken))
                throw new InvalidOperationException("Resposta de token inválida do Twitch.");

            // Guarda token em memória.
            _token = data.AccessToken;

            // Calcula expiração com folga de 60 segundos.
            _tokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, data.ExpiresIn - 60));

            return _token!;
        }
    }
}