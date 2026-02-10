using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// ✅ Cliente IGDB (apicalypse)
    /// - Faz POST para endpoints v4 (ex: /games)
    /// - Mete headers Client-ID e Authorization Bearer
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

            // Corpo (Apicalypse) - enviado como texto/plain
            var body = $@"
fields id,name,slug,first_release_date,cover.image_id;
search ""{EscapeApicalypseString(term)}"";
limit {limit};
";

            using var req = await CreateIgdbRequestAsync("games", body, ct);
            using var resp = await _http.SendAsync(req, ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB games search failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            var data = JsonSerializer.Deserialize<List<IgdbGameDto>>(raw, JsonOpts);
            return data ?? new List<IgdbGameDto>();
        }

        /// <summary>
        /// ✅ Detalhes de 1 jogo (para a página /games/{id})
        /// </summary>
        public async Task<IgdbGameDetailsDto?> GetGameDetailsAsync(int id, CancellationToken ct = default)
        {
            if (id <= 0) return null;

            var body = $@"
fields
id,name,slug,summary,storyline,first_release_date,
cover.image_id,
artworks.image_id,
screenshots.image_id,
genres.name,
platforms.name,
involved_companies.company.name,
involved_companies.developer,
involved_companies.publisher,
aggregated_rating,aggregated_rating_count,
rating,rating_count,
hypes,follows;
where id = {id};
limit 1;
";

            using var req = await CreateIgdbRequestAsync("games", body, ct);
            using var resp = await _http.SendAsync(req, ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB /games failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            var data = JsonSerializer.Deserialize<List<IgdbGameDetailsDto>>(raw, JsonOpts);
            return data?.FirstOrDefault();
        }

        /// <summary>
        /// Monta request com token válido + headers IGDB.
        /// ✅ Usa URL absoluto para evitar problemas com proxies/headers a serem reescritos.
        /// </summary>
        private async Task<HttpRequestMessage> CreateIgdbRequestAsync(string endpoint, string body, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_options.ClientId))
                throw new InvalidOperationException("IGDB ClientId em falta no appsettings.json.");

            if (_http.BaseAddress == null)
                throw new InvalidOperationException("IgdbClient HttpClient BaseAddress não configurado (Program.cs).");

            var token = await _tokenProvider.GetAccessTokenAsync(ct);

            // ✅ URL absoluto (garante que vai mesmo para https://api.igdb.com/v4/<endpoint>)
            var url = new Uri(_http.BaseAddress, endpoint.TrimStart('/'));

            var req = new HttpRequestMessage(HttpMethod.Post, url);

            // Headers pedidos pelo IGDB
            req.Headers.Remove("Client-ID");
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
