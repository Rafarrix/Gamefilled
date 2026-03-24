using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// Responsável por obter e guardar em memória o token OAuth do Twitch,
    /// necessário para aceder à API IGDB.
    ///
    /// Responsabilidades:
    /// - Pedir token novo quando necessário
    /// - Reutilizar token enquanto ainda for válido
    /// - Aplicar pequena margem de segurança antes da expiração
    /// </summary>
    public class IgdbTokenProvider
    {
        /// <summary>
        /// HttpClient usado para falar com o endpoint OAuth do Twitch.
        /// </summary>
        private readonly HttpClient _http;

        /// <summary>
        /// Configuração com ClientId e ClientSecret.
        /// </summary>
        private readonly IgdbOptions _options;

        /// <summary>
        /// Token atualmente guardado em memória.
        /// </summary>
        private string? _token;

        /// <summary>
        /// Data/hora UTC em que o token deixa de ser considerado válido.
        /// </summary>
        private DateTimeOffset _tokenExpiresAtUtc;

        /// <summary>
        /// Construtor com dependências injetadas.
        /// </summary>
        public IgdbTokenProvider(HttpClient http, IOptions<IgdbOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        /// <summary>
        /// Devolve sempre um token válido.
        /// Se o token atual ainda não expirou, reutiliza-o.
        /// Caso contrário, pede um novo token ao Twitch.
        /// </summary>
        public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
        {
            // Se já houver token e ainda for válido, reutiliza.
            if (!string.IsNullOrWhiteSpace(_token) && DateTimeOffset.UtcNow < _tokenExpiresAtUtc)
                return _token!;

            // Validação da configuração obrigatória.
            if (string.IsNullOrWhiteSpace(_options.ClientId) || string.IsNullOrWhiteSpace(_options.ClientSecret))
                throw new InvalidOperationException("IGDB ClientId/ClientSecret em falta no appsettings.json.");

            // Endpoint OAuth do Twitch com client credentials.
            var url =
                $"https://id.twitch.tv/oauth2/token" +
                $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
                $"&client_secret={Uri.EscapeDataString(_options.ClientSecret)}" +
                $"&grant_type=client_credentials";

            // O Twitch aceita POST sem body, com parâmetros na querystring.
            using var resp = await _http.PostAsync(url, content: null, ct);

            // Lança exceção automática em caso de erro HTTP.
            resp.EnsureSuccessStatusCode();

            // Lê a resposta JSON para o DTO correspondente.
            var data = await resp.Content.ReadFromJsonAsync<TwitchTokenResponse>(cancellationToken: ct);

            // Garante que a resposta contém token válido.
            if (data == null || string.IsNullOrWhiteSpace(data.AccessToken))
                throw new InvalidOperationException("Resposta de token inválida do Twitch.");

            // Guarda o token em memória.
            _token = data.AccessToken;

            // Calcula a expiração com folga de 60 segundos.
            // Isto evita falhar no exato momento limite da validade.
            _tokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, data.ExpiresIn - 60));

            return _token!;
        }
    }
}