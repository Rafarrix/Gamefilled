using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// Cliente de acesso à API IGDB.
    ///
    /// Responsabilidades:
    /// - Construir pedidos HTTP para a IGDB
    /// - Pedir token válido ao IgdbTokenProvider
    /// - Fazer pesquisas de jogos
    /// - Obter detalhes de um jogo
    /// - Obter listas de jogos para secções do site
    /// - Desserializar JSON em DTOs C#
    /// </summary>
    public class IgdbClient
    {
        /// <summary>
        /// HttpClient usado para fazer pedidos à API IGDB.
        /// </summary>
        private readonly HttpClient _http;

        /// <summary>
        /// Provider responsável por fornecer um token válido de acesso.
        /// </summary>
        private readonly IgdbTokenProvider _tokenProvider;

        /// <summary>
        /// Opções/configuração da IGDB vindas do appsettings.
        /// </summary>
        private readonly IgdbOptions _options;

        /// <summary>
        /// Opções globais de serialização JSON.
        /// JsonSerializerDefaults.Web usa comportamento mais adequado para APIs web.
        /// </summary>
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// Construtor com dependências injetadas pelo container.
        /// </summary>
        public IgdbClient(HttpClient http, IgdbTokenProvider tokenProvider, IOptions<IgdbOptions> options)
        {
            _http = http;
            _tokenProvider = tokenProvider;
            _options = options.Value;
        }

        /* =====================================================================
           PESQUISA DE JOGOS
           ===================================================================== */

        /// <summary>
        /// Pesquisa jogos pelo nome/termo indicado.
        /// </summary>
        /// <param name="term">Texto a pesquisar.</param>
        /// <param name="limit">Número máximo de resultados.</param>
        /// <param name="ct">CancellationToken do pedido.</param>
        /// <returns>Lista de jogos simplificados.</returns>
        public async Task<List<IgdbGameDto>> SearchGamesAsync(string term, int limit = 10, CancellationToken ct = default)
        {
            // Normaliza o termo e remove espaços desnecessários.
            term = (term ?? string.Empty).Trim();

            // Evita pedidos à API para termos demasiado curtos.
            if (term.Length < 2)
                return new List<IgdbGameDto>();

            // Corpo APICalypse para pesquisa de jogos.
            var body = $@"
fields id,name,slug,first_release_date,cover.image_id;
search ""{EscapeApicalypseString(term)}"";
limit {limit};
";

            // Cria o pedido HTTP já autenticado.
            using var req = await CreateIgdbRequestAsync("games", body, ct);

            // Envia o pedido.
            using var resp = await _http.SendAsync(req, ct);

            // Lê o corpo da resposta em texto.
            var raw = await resp.Content.ReadAsStringAsync(ct);

            // Se a API devolver erro, lança exceção com detalhe útil para debugging.
            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB games search failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            // Converte JSON para lista de DTOs.
            var data = JsonSerializer.Deserialize<List<IgdbGameDto>>(raw, JsonOpts);

            // Garante nunca devolver null.
            return data ?? new List<IgdbGameDto>();
        }

        /* =====================================================================
           DETALHE DE JOGO
           ===================================================================== */

        /// <summary>
        /// Obtém os detalhes completos de um jogo por ID.
        /// </summary>
        /// <param name="id">ID numérico do jogo na IGDB.</param>
        /// <param name="ct">CancellationToken do pedido.</param>
        /// <returns>Detalhes do jogo ou null se o ID for inválido/não existir.</returns>
        public async Task<IgdbGameDetailsDto?> GetGameDetailsAsync(int id, CancellationToken ct = default)
        {
            // Proteção contra IDs inválidos.
            if (id <= 0)
                return null;

            // Corpo APICalypse com todos os campos necessários
            // para a página de detalhe do jogo.
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

            // Como usamos limit 1, devolvemos só o primeiro elemento.
            return data?.FirstOrDefault();
        }

        /* =====================================================================
           LISTAS DE JOGOS PARA A HOME / SECÇÕES
           ===================================================================== */

        /// <summary>
        /// Obtém jogos "trending".
        /// Atualmente usa rating_count como aproximação de popularidade recente.
        /// </summary>
        public async Task<List<IgdbGameDto>> GetTrendingGamesAsync(int limit = 12, CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var fiveYearsAgo = DateTimeOffset.UtcNow.AddYears(-5).ToUnixTimeSeconds();

            var body = $@"
fields id,name,slug,first_release_date,cover.image_id;
where cover != null
  & first_release_date != null
  & first_release_date >= {fiveYearsAgo}
  & first_release_date <= {now}
  & rating_count != null
  & rating_count > 10;
sort rating_count desc;
limit {limit};
";

            using var req = await CreateIgdbRequestAsync("games", body, ct);
            using var resp = await _http.SendAsync(req, ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB trending failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            var data = JsonSerializer.Deserialize<List<IgdbGameDto>>(raw, JsonOpts);
            return data ?? new List<IgdbGameDto>();
        }

        /// <summary>
        /// Obtém lançamentos recentes dos últimos 2 anos.
        /// </summary>
        public async Task<List<IgdbGameDto>> GetRecentReleasesAsync(int limit = 12, CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var twoYearsAgo = DateTimeOffset.UtcNow.AddYears(-2).ToUnixTimeSeconds();

            var body = $@"
fields id,name,slug,first_release_date,cover.image_id;
where cover != null
  & first_release_date != null
  & first_release_date >= {twoYearsAgo}
  & first_release_date <= {now};
sort first_release_date desc;
limit {limit};
";

            using var req = await CreateIgdbRequestAsync("games", body, ct);
            using var resp = await _http.SendAsync(req, ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB recent releases failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            var data = JsonSerializer.Deserialize<List<IgdbGameDto>>(raw, JsonOpts);
            return data ?? new List<IgdbGameDto>();
        }

        /// <summary>
        /// Obtém jogos mais bem classificados com base no aggregated_rating.
        /// </summary>
        public async Task<List<IgdbGameDto>> GetTopRatedGamesAsync(int limit = 12, CancellationToken ct = default)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var body = $@"
fields id,name,slug,first_release_date,cover.image_id;
where cover != null
  & first_release_date != null
  & first_release_date <= {now}
  & aggregated_rating != null
  & aggregated_rating_count != null
  & aggregated_rating_count > 20;
sort aggregated_rating desc;
limit {limit};
";

            using var req = await CreateIgdbRequestAsync("games", body, ct);
            using var resp = await _http.SendAsync(req, ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB top rated failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            var data = JsonSerializer.Deserialize<List<IgdbGameDto>>(raw, JsonOpts);
            return data ?? new List<IgdbGameDto>();
        }

        /* =====================================================================
           OBTENÇÃO DE JOGOS POR IDS
           ===================================================================== */

        /// <summary>
        /// Obtém uma lista de jogos a partir de vários IDs.
        /// Útil, por exemplo, para favoritos guardados em base de dados.
        /// </summary>
        public async Task<List<IgdbGameDto>> GetGamesByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            // Limpa a lista:
            // - remove IDs inválidos
            // - remove duplicados
            var cleanIds = ids
                .Where(x => x > 0)
                .Distinct()
                .ToList();

            // Se não houver IDs válidos, devolve lista vazia.
            if (!cleanIds.Any())
                return new List<IgdbGameDto>();

            // Converte a lista para CSV para usar no APICalypse.
            var idsCsv = string.Join(",", cleanIds);

            var body = $@"
fields id,name,slug,first_release_date,cover.image_id;
where id = ({idsCsv});
limit {cleanIds.Count};
";

            using var req = await CreateIgdbRequestAsync("games", body, ct);
            using var resp = await _http.SendAsync(req, ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new HttpRequestException($"IGDB games by ids failed: {(int)resp.StatusCode} {resp.ReasonPhrase}\nBody: {raw}");

            var data = JsonSerializer.Deserialize<List<IgdbGameDto>>(raw, JsonOpts);
            return data ?? new List<IgdbGameDto>();
        }

        /* =====================================================================
           HELPERS INTERNOS
           ===================================================================== */

        /// <summary>
        /// Cria um HttpRequestMessage autenticado para um endpoint da IGDB.
        /// </summary>
        /// <param name="endpoint">Endpoint relativo, ex.: "games".</param>
        /// <param name="body">Corpo APICalypse.</param>
        /// <param name="ct">CancellationToken.</param>
        /// <returns>Pedido HTTP pronto a enviar.</returns>
        private async Task<HttpRequestMessage> CreateIgdbRequestAsync(string endpoint, string body, CancellationToken ct)
        {
            // Validação de configuração obrigatória.
            if (string.IsNullOrWhiteSpace(_options.ClientId))
                throw new InvalidOperationException("IGDB ClientId em falta no appsettings.json.");

            // Garante que o HttpClient foi configurado corretamente no Program.cs.
            if (_http.BaseAddress == null)
                throw new InvalidOperationException("IgdbClient HttpClient BaseAddress não configurado (Program.cs).");

            // Obtém token válido.
            var token = await _tokenProvider.GetAccessTokenAsync(ct);

            // Constrói URL final com base no endpoint.
            var url = new Uri(_http.BaseAddress, endpoint.TrimStart('/'));

            var req = new HttpRequestMessage(HttpMethod.Post, url);

            // Define cabeçalho exigido pela IGDB.
            req.Headers.Remove("Client-ID");
            req.Headers.Add("Client-ID", _options.ClientId);

            // Define token Bearer.
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // APICalypse vai em texto simples.
            req.Content = new StringContent(body, Encoding.UTF8, "text/plain");

            return req;
        }

        /// <summary>
        /// Escapa caracteres problemáticos para o corpo APICalypse.
        /// Evita quebrar a query quando o utilizador escreve aspas ou barras.
        /// </summary>
        private static string EscapeApicalypseString(string s)
            => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}