using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// ✅ Cliente IGDB (apicalypse)
    /// - Faz POST para endpoints v4 (ex: /games)
    /// - Mete headers Client-ID e Authorization Bearer :contentReference[oaicite:5]{index=5}
    /// </summary>
    public class IgdbClient
    {
        private readonly HttpClient _http;
        private readonly IgdbTokenProvider _tokenProvider;
        private readonly IgdbOptions _options;

        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        public IgdbClient(HttpClient http, IgdbTokenProvider tokenProvider, IOptions<IgdbOptions> options)
        {
            _http = http;
            _tokenProvider = tokenProvider;
            _options = options.Value;
        }

        /// <summary>
        /// Search básico de jogos (para autocomplete e página de search).
        /// </summary>
        public async Task<List<IgdbGameDto>> SearchGamesAsync(string term, int limit = 10, CancellationToken ct = default)
        {
            term = (term ?? string.Empty).Trim();
            if (term.Length < 2) return new List<IgdbGameDto>();

            // Corpo (Apicalypse) - enviado como texto/plain :contentReference[oaicite:6]{index=6}
            // Dica: filtrar category para evitar DLC/expansões pode ser feito depois.
            var body = $@"
fields id,name,slug,first_release_date,cover.image_id;
search ""{EscapeApicalypseString(term)}"";
limit {limit};
";

            using var req = await CreateIgdbRequestAsync("/games", body, ct);
            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            var data = JsonSerializer.Deserialize<List<IgdbGameDto>>(json, JsonOpts);

            return data ?? new List<IgdbGameDto>();
        }

        /// <summary>
        /// Monta request com token válido + headers IGDB.
        /// </summary>
        private async Task<HttpRequestMessage> CreateIgdbRequestAsync(string endpoint, string body, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_options.ClientId))
                throw new InvalidOperationException("IGDB ClientId em falta no appsettings.json.");

            var token = await _tokenProvider.GetAccessTokenAsync(ct);

            var req = new HttpRequestMessage(HttpMethod.Post, endpoint);

            // Headers pedidos pelo IGDB :contentReference[oaicite:7]{index=7}
            req.Headers.Add("Client-ID", _options.ClientId);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Apicalypse como texto
            req.Content = new StringContent(body, Encoding.UTF8, "text/plain");

            return req;
        }

        /// <summary>
        /// Apicalypse usa aspas; isto evita quebra do body.
        /// </summary>
        private static string EscapeApicalypseString(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
