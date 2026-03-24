using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace Gamefilled.Pages.games.lib
{
    /// <summary>
    /// Classe base comum para as páginas de biblioteca em /games/lib/*
    ///
    /// Responsabilidades:
    /// - tratar paginação (pageNumber/pageSize)
    /// - tratar direção de ordenação (dir=asc|desc)
    /// - carregar listas de jogos da IGDB conforme o tipo de ordenação
    /// - preparar dados já prontos para a View
    ///
    /// Sorts suportados:
    /// - popular
    /// - trending
    /// - top-rated
    /// - release-date
    /// - title
    /// - avg-play
    /// - avg-finish
    /// </summary>
    public abstract class _GamesLibBase : PageModel
    {
        /* =====================================================================
           DEPENDÊNCIAS
           ===================================================================== */

        /// <summary>
        /// Configuração da aplicação.
        /// </summary>
        protected readonly IConfiguration _cfg;

        /// <summary>
        /// Logger usado pela página concreta.
        /// </summary>
        protected readonly ILogger _logger;

        /// <summary>
        /// Factory de HttpClient.
        /// </summary>
        protected readonly IHttpClientFactory _http;

        /// <summary>
        /// Construtor base.
        /// </summary>
        protected _GamesLibBase(IConfiguration cfg, ILogger logger, IHttpClientFactory http)
        {
            _cfg = cfg;
            _logger = logger;
            _http = http;
        }

        /* =====================================================================
           INPUTS DA QUERY STRING
           ===================================================================== */

        /// <summary>
        /// Número da página atual.
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        /// <summary>
        /// Quantidade de jogos por página.
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 36;

        /// <summary>
        /// Direção da ordenação: asc ou desc.
        /// </summary>
        [BindProperty(SupportsGet = true)]
        public string Dir { get; set; } = "desc";

        /* =====================================================================
           OUTPUT PARA A VIEW
           ===================================================================== */

        /// <summary>
        /// Chave de ordenação atual.
        /// </summary>
        public string SortKey { get; protected set; } = "popular";

        /// <summary>
        /// Título mostrado na UI para o filtro atual.
        /// </summary>
        public string PageTitle { get; protected set; } = "Popularity";

        /// <summary>
        /// Número total estimado de resultados.
        /// </summary>
        public int TotalCount { get; protected set; }

        /// <summary>
        /// Número total de páginas.
        /// </summary>
        public int TotalPages { get; protected set; }

        /// <summary>
        /// Jogos preparados para a grelha da biblioteca.
        /// </summary>
        public List<GameCard> Games { get; protected set; } = new();

        /* =====================================================================
           DEBUG / DIAGNÓSTICO
           ===================================================================== */

        /// <summary>
        /// Última query APICalypse enviada.
        /// </summary>
        public string? LastIgdbQuery { get; protected set; }

        /// <summary>
        /// Último status code devolvido pela IGDB.
        /// </summary>
        public int? LastIgdbStatus { get; protected set; }

        /// <summary>
        /// Último body JSON/texto devolvido pela IGDB.
        /// </summary>
        public string? LastIgdbBody { get; protected set; }

        /* =====================================================================
           VIEW MODEL INTERNO PARA CARDS DE JOGO
           ===================================================================== */

        /// <summary>
        /// Representa um jogo já simplificado/preparado para a UI da biblioteca.
        /// </summary>
        public class GameCard
        {
            /// <summary>
            /// ID do jogo na IGDB.
            /// </summary>
            public long Id { get; set; }

            /// <summary>
            /// Nome do jogo.
            /// </summary>
            public string Name { get; set; } = "";

            /// <summary>
            /// Slug do jogo.
            /// </summary>
            public string Slug { get; set; } = "";

            /// <summary>
            /// ImageId da capa.
            /// </summary>
            public string? CoverImageId { get; set; }

            /// <summary>
            /// Data de lançamento em UNIX timestamp.
            /// </summary>
            public long? FirstReleaseDate { get; set; }

            /// <summary>
            /// Ano de lançamento já extraído.
            /// </summary>
            public int? Year { get; set; }

            /// <summary>
            /// Rating total.
            /// </summary>
            public double? TotalRating { get; set; }

            /// <summary>
            /// Número de ratings.
            /// </summary>
            public int? TotalRatingCount { get; set; }

            /// <summary>
            /// Tempo médio de jogo (normally) em segundos.
            /// </summary>
            public int? AvgPlaySeconds { get; set; }

            /// <summary>
            /// Tempo médio para completar (completely) em segundos.
            /// </summary>
            public int? AvgFinishSeconds { get; set; }

            /// <summary>
            /// Texto formatado em horas para AvgPlaySeconds.
            /// </summary>
            public string? AvgPlayHoursText => FormatSecondsToHours(AvgPlaySeconds);

            /// <summary>
            /// Texto formatado em horas para AvgFinishSeconds.
            /// </summary>
            public string? AvgFinishHoursText => FormatSecondsToHours(AvgFinishSeconds);

            /// <summary>
            /// Data formatada para a interface.
            /// </summary>
            public string? ReleaseDateText => FormatUnixDate(FirstReleaseDate);

            /// <summary>
            /// Rating formatado para mostrar debaixo da capa.
            /// </summary>
            public string? RatingText => TotalRating.HasValue ? $"★ {TotalRating.Value:0.#}" : null;

            /// <summary>
            /// URL completa da cover do jogo.
            /// </summary>
            public string? CoverUrl =>
                string.IsNullOrWhiteSpace(CoverImageId)
                    ? null
                    : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{CoverImageId}.jpg";

            /// <summary>
            /// Converte segundos em texto de horas.
            /// </summary>
            private static string? FormatSecondsToHours(int? seconds)
            {
                if (seconds == null || seconds <= 0) return null;

                double hours = seconds.Value / 3600.0;
                return $"{hours:0.#}h";
            }

            /// <summary>
            /// Converte UNIX timestamp em data formatada pt-PT.
            /// </summary>
            private static string? FormatUnixDate(long? unixSeconds)
            {
                if (unixSeconds == null || unixSeconds <= 0) return null;

                try
                {
                    var dt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value).UtcDateTime;
                    var pt = CultureInfo.GetCultureInfo("pt-PT");

                    return dt.ToString("dd MMM yyyy", pt);
                }
                catch
                {
                    return null;
                }
            }
        }

        /* =====================================================================
           CONFIGURAÇÃO ESPECÍFICA DA SUBCLASSE
           ===================================================================== */

        /// <summary>
        /// Cada página derivada define aqui o seu SortKey e PageTitle.
        /// </summary>
        protected abstract void Configure();

        /* =====================================================================
           HANDLER GET PRINCIPAL
           ===================================================================== */

        public async Task OnGetAsync()
        {
            // Permite à subclasse definir o contexto da página.
            Configure();

            // Normalização dos parâmetros recebidos.
            if (PageNumber < 1) PageNumber = 1;
            if (PageSize < 12) PageSize = 12;
            if (PageSize > 60) PageSize = 60;

            Dir = NormalizeDir(Dir);

            // Tenta ler configuração IGDB em duas variantes de chave.
            var clientId = _cfg["IGDB:ClientId"] ?? _cfg["Igdb:ClientId"];
            var clientSecret = _cfg["IGDB:ClientSecret"] ?? _cfg["Igdb:ClientSecret"];

            // Se não houver credenciais, devolve lista vazia sem rebentar a página.
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                _logger.LogWarning("IGDB credentials em falta. Procurei IGDB:* e Igdb:*.");
                Games = new();
                TotalCount = 0;
                TotalPages = 0;
                return;
            }

            try
            {
                // Obtém token OAuth do Twitch.
                var token = await GetTwitchToken(clientId, clientSecret);

                // Configura cliente autenticado para a IGDB.
                var client = _http.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Client-ID", clientId);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Sorts baseados em game_time_to_beats.
                if (SortKey == "avg-play" || SortKey == "avg-finish")
                {
                    await LoadByTimeToBeatAsync(client);
                    return;
                }

                // Sorts baseados em popularity_primitives.
                if (SortKey == "popular" || SortKey == "trending")
                {
                    await LoadByPopScoreAsync(client, SortKey);
                    return;
                }

                // Restantes sorts usam /games diretamente.
                await LoadByGamesAsync(client);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro a carregar IGDB em GamesLib");
                Games = new();
                TotalCount = 0;
                TotalPages = 0;
            }
        }

        /* =====================================================================
           A) CARREGAMENTO VIA /GAMES
           Usado para:
           - top-rated
           - release-date
           - title
           ===================================================================== */

        private async Task LoadByGamesAsync(HttpClient client)
        {
            var offset = (PageNumber - 1) * PageSize;

            var fields = "fields id,name,slug,cover.image_id,first_release_date,total_rating,total_rating_count;";

            var (where, sortLine) = BuildWhereAndSortForGames(SortKey, Dir);

            var query = fields + where + sortLine + $"limit {PageSize};" + $"offset {offset};";
            LastIgdbQuery = query;

            var content = new StringContent(query, Encoding.UTF8, "text/plain");
            var res = await client.PostAsync("https://api.igdb.com/v4/games", content);
            var json = await res.Content.ReadAsStringAsync();

            LastIgdbStatus = (int)res.StatusCode;
            LastIgdbBody = json;

            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "IGDB /games falhou: {Status} | Body: {Body} | Query: {Query}",
                    res.StatusCode, json, query
                );

                Games = new();
                TotalCount = 0;
                TotalPages = 0;
                return;
            }

            using var doc = JsonDocument.Parse(json);
            Games = ParseGames(doc.RootElement);

            // Count best effort para paginação.
            TotalCount = await TryGetCount(client, "https://api.igdb.com/v4/games/count", where);
            TotalPages = (TotalCount <= 0) ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        }

        /// <summary>
        /// Constrói o bloco WHERE e SORT para os sorts baseados em /games.
        /// </summary>
        private (string where, string sortLine) BuildWhereAndSortForGames(string sortKey, string dir)
        {
            var baseWhere = "where cover != null & slug != null & name != null";

            if (sortKey == "top-rated")
            {
                var where = baseWhere + " & total_rating != null & total_rating_count != null & total_rating_count >= 50;";
                return (where, $"sort total_rating {dir};");
            }

            if (sortKey == "release-date")
            {
                var where = baseWhere + " & first_release_date != null;";
                return (where, $"sort first_release_date {dir};");
            }

            if (sortKey == "title")
            {
                return (baseWhere + ";", $"sort name {dir};");
            }

            // Fallback seguro.
            return (baseWhere + ";", $"sort first_release_date {dir};");
        }

        /* =====================================================================
           B) CARREGAMENTO VIA POPSCORE / POPULARITY_PRIMITIVES
           Usado para:
           - popular
           - trending
           ===================================================================== */

        private async Task LoadByPopScoreAsync(HttpClient client, string mode)
        {
            var offset = (PageNumber - 1) * PageSize;

            // Primeiro resolve o tipo de popularidade a usar.
            var typeId = await TryResolvePopularityTypeIdAsync(client, mode);

            // Se não conseguir resolver, usa fallback.
            if (typeId == null)
            {
                await LoadFallbackPopularTrendingAsync(client, mode);
                return;
            }

            var primFields = "fields game_id,value,popularity_type;";
            var primWhere = $"where popularity_type = {typeId.Value} & game_id != null;";
            var primSort = $"sort value {Dir};";

            var primQuery = primFields + primWhere + primSort + $"limit {PageSize};" + $"offset {offset};";
            LastIgdbQuery = primQuery;

            var primContent = new StringContent(primQuery, Encoding.UTF8, "text/plain");
            var primRes = await client.PostAsync("https://api.igdb.com/v4/popularity_primitives", primContent);
            var primJson = await primRes.Content.ReadAsStringAsync();

            LastIgdbStatus = (int)primRes.StatusCode;
            LastIgdbBody = primJson;

            if (!primRes.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "IGDB /popularity_primitives falhou: {Status} | Body: {Body} | Query: {Query}",
                    primRes.StatusCode, primJson, primQuery
                );

                await LoadFallbackPopularTrendingAsync(client, mode);
                return;
            }

            var orderedIds = new List<long>();

            using (var doc = JsonDocument.Parse(primJson))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var row in doc.RootElement.EnumerateArray())
                    {
                        long gameId = row.TryGetProperty("game_id", out var gEl) && gEl.ValueKind == JsonValueKind.Number
                            ? gEl.GetInt64()
                            : 0;

                        if (gameId > 0)
                            orderedIds.Add(gameId);
                    }
                }
            }

            // Se não houver resultados utilizáveis, usa fallback.
            if (orderedIds.Count == 0)
            {
                await LoadFallbackPopularTrendingAsync(client, mode);
                return;
            }

            // Vai buscar os dados completos dos jogos pelos IDs.
            var games = await FetchGamesByIdsAsync(client, orderedIds);

            // Reordena os jogos pela ordem original devolvida por popularity_primitives.
            var byId = games.ToDictionary(x => x.Id, x => x);
            var ordered = new List<GameCard>();

            foreach (var id in orderedIds)
            {
                if (byId.TryGetValue(id, out var g))
                    ordered.Add(g);
            }

            Games = ordered;

            // Count best effort.
            TotalCount = await TryGetCount(client, "https://api.igdb.com/v4/popularity_primitives/count", primWhere);
            TotalPages = (TotalCount <= 0) ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        }

        /// <summary>
        /// Tenta descobrir qual popularity_type usar consoante o modo.
        /// </summary>
        private async Task<int?> TryResolvePopularityTypeIdAsync(HttpClient client, string mode)
        {
            var wanted = (mode == "trending")
                ? new[] { "Playing", "Want to Play", "Played", "Visits" }
                : new[] { "Visits", "Want to Play", "Playing", "Played" };

            var query = "fields id,name; limit 500;";
            var content = new StringContent(query, Encoding.UTF8, "text/plain");
            var res = await client.PostAsync("https://api.igdb.com/v4/popularity_types", content);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            var types = new List<(int id, string name)>();

            foreach (var t in doc.RootElement.EnumerateArray())
            {
                if (!t.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number)
                    continue;

                var id = idEl.GetInt32();
                var name = t.TryGetProperty("name", out var nEl) ? (nEl.GetString() ?? "") : "";

                if (!string.IsNullOrWhiteSpace(name))
                    types.Add((id, name));
            }

            foreach (var w in wanted)
            {
                var hit = types.FirstOrDefault(x => x.name.Contains(w, StringComparison.OrdinalIgnoreCase));
                if (hit.id != 0) return hit.id;
            }

            return types.Count > 0 ? types[0].id : (int?)null;
        }

        /// <summary>
        /// Fallback para popular/trending caso o endpoint de PopScore falhe.
        /// </summary>
        private async Task LoadFallbackPopularTrendingAsync(HttpClient client, string mode)
        {
            var offset = (PageNumber - 1) * PageSize;

            var fields = "fields id,name,slug,cover.image_id,first_release_date,total_rating,total_rating_count;";
            string where = "where cover != null & slug != null & name != null;";
            string sort;

            if (mode == "popular")
            {
                where = "where cover != null & slug != null & name != null & total_rating_count != null & total_rating_count >= 100;";
                sort = $"sort total_rating {Dir};";
            }
            else
            {
                var twoYearsAgo = DateTimeOffset.UtcNow.AddDays(-365 * 2).ToUnixTimeSeconds();
                where = $"where cover != null & slug != null & name != null & first_release_date != null & first_release_date > {twoYearsAgo};";
                sort = $"sort first_release_date {Dir};";
            }

            var query = fields + where + sort + $"limit {PageSize};" + $"offset {offset};";
            LastIgdbQuery = query;

            var content = new StringContent(query, Encoding.UTF8, "text/plain");
            var res = await client.PostAsync("https://api.igdb.com/v4/games", content);
            var json = await res.Content.ReadAsStringAsync();

            LastIgdbStatus = (int)res.StatusCode;
            LastIgdbBody = json;

            if (!res.IsSuccessStatusCode)
            {
                Games = new();
                TotalCount = 0;
                TotalPages = 0;
                return;
            }

            using var doc = JsonDocument.Parse(json);
            Games = ParseGames(doc.RootElement);

            TotalCount = await TryGetCount(client, "https://api.igdb.com/v4/games/count", where);
            TotalPages = (TotalCount <= 0) ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        }

        /* =====================================================================
           C) CARREGAMENTO VIA GAME_TIME_TO_BEATS
           Usado para:
           - avg-play
           - avg-finish
           ===================================================================== */

        private async Task LoadByTimeToBeatAsync(HttpClient client)
        {
            var offset = (PageNumber - 1) * PageSize;

            var ttbFields = "fields game_id,normally,completely;";

            string where;
            string sort;

            if (SortKey == "avg-play")
            {
                where = "where game_id != null & normally != null & normally > 3600 & normally < 720000;";
                sort = $"sort normally {Dir};";
            }
            else
            {
                where = "where game_id != null & completely != null & completely > 3600 & completely < 1080000;";
                sort = $"sort completely {Dir};";
            }

            var ttbQuery = ttbFields + where + sort + $"limit {PageSize};" + $"offset {offset};";
            LastIgdbQuery = ttbQuery;

            var ttbContent = new StringContent(ttbQuery, Encoding.UTF8, "text/plain");
            var ttbRes = await client.PostAsync("https://api.igdb.com/v4/game_time_to_beats", ttbContent);
            var ttbJson = await ttbRes.Content.ReadAsStringAsync();

            LastIgdbStatus = (int)ttbRes.StatusCode;
            LastIgdbBody = ttbJson;

            if (!ttbRes.IsSuccessStatusCode)
            {
                Games = new();
                TotalCount = 0;
                TotalPages = 0;
                return;
            }

            var orderedIds = new List<long>();
            var timesByGameId = new Dictionary<long, (int? normally, int? completely)>();

            using (var doc = JsonDocument.Parse(ttbJson))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var row in doc.RootElement.EnumerateArray())
                    {
                        long gameId = row.TryGetProperty("game_id", out var gEl) && gEl.ValueKind == JsonValueKind.Number
                            ? gEl.GetInt64()
                            : 0;

                        if (gameId <= 0) continue;

                        int? normally = row.TryGetProperty("normally", out var nEl) && nEl.ValueKind == JsonValueKind.Number
                            ? nEl.GetInt32()
                            : (int?)null;

                        int? completely = row.TryGetProperty("completely", out var cEl) && cEl.ValueKind == JsonValueKind.Number
                            ? cEl.GetInt32()
                            : (int?)null;

                        orderedIds.Add(gameId);
                        timesByGameId[gameId] = (normally, completely);
                    }
                }
            }

            if (orderedIds.Count == 0)
            {
                Games = new();
                TotalCount = 0;
                TotalPages = 0;
                return;
            }

            var games = await FetchGamesByIdsAsync(client, orderedIds);

            var byId = games.ToDictionary(x => x.Id, x => x);
            var orderedGames = new List<GameCard>();

            foreach (var id in orderedIds)
            {
                if (byId.TryGetValue(id, out var game))
                {
                    if (timesByGameId.TryGetValue(id, out var t))
                    {
                        game.AvgPlaySeconds = t.normally;
                        game.AvgFinishSeconds = t.completely;
                    }

                    orderedGames.Add(game);
                }
            }

            Games = orderedGames;

            TotalCount = await TryGetCount(client, "https://api.igdb.com/v4/game_time_to_beats/count", where);
            TotalPages = (TotalCount <= 0) ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        }

        /// <summary>
        /// Vai buscar jogos pelos IDs e converte-os em GameCard.
        /// </summary>
        private async Task<List<GameCard>> FetchGamesByIdsAsync(HttpClient client, List<long> ids)
        {
            var idList = string.Join(",", ids);

            var fields = "fields id,name,slug,cover.image_id,first_release_date,total_rating,total_rating_count;";
            var where =
                "where id = (" + idList + ")" +
                " & cover != null & slug != null & name != null" +
                " & game_modes != null & game_modes != (5);";

            var query = fields + where + "limit 500;";
            var content = new StringContent(query, Encoding.UTF8, "text/plain");

            var res = await client.PostAsync("https://api.igdb.com/v4/games", content);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return new List<GameCard>();

            using var doc = JsonDocument.Parse(json);
            return ParseGames(doc.RootElement);
        }

        /* =====================================================================
           HELPERS GERAIS
           ===================================================================== */

        /// <summary>
        /// Converte o JSON da IGDB em lista de GameCard.
        /// </summary>
        private List<GameCard> ParseGames(JsonElement arr)
        {
            var list = new List<GameCard>();
            if (arr.ValueKind != JsonValueKind.Array) return list;

            foreach (var g in arr.EnumerateArray())
            {
                var card = new GameCard
                {
                    Id = g.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number ? idEl.GetInt64() : 0,
                    Name = g.TryGetProperty("name", out var nEl) ? (nEl.GetString() ?? "") : "",
                    Slug = g.TryGetProperty("slug", out var sEl) ? (sEl.GetString() ?? "") : "",
                    TotalRating = g.TryGetProperty("total_rating", out var trEl) && trEl.ValueKind == JsonValueKind.Number ? trEl.GetDouble() : null,
                    TotalRatingCount = g.TryGetProperty("total_rating_count", out var tcEl) && tcEl.ValueKind == JsonValueKind.Number ? tcEl.GetInt32() : null,
                };

                if (g.TryGetProperty("first_release_date", out var frEl) && frEl.ValueKind == JsonValueKind.Number)
                {
                    card.FirstReleaseDate = frEl.GetInt64();

                    try
                    {
                        card.Year = DateTimeOffset.FromUnixTimeSeconds(card.FirstReleaseDate.Value).Year;
                    }
                    catch
                    {
                    }
                }

                if (g.TryGetProperty("cover", out var covEl) && covEl.ValueKind == JsonValueKind.Object)
                {
                    if (covEl.TryGetProperty("image_id", out var imgEl))
                        card.CoverImageId = imgEl.GetString();
                }

                if (!string.IsNullOrWhiteSpace(card.Name) && !string.IsNullOrWhiteSpace(card.Slug))
                    list.Add(card);
            }

            return list;
        }

        /// <summary>
        /// Tenta obter a contagem total no endpoint /count.
        /// Se falhar, devolve 0.
        /// </summary>
        private async Task<int> TryGetCount(HttpClient client, string countEndpoint, string where)
        {
            try
            {
                var content = new StringContent(where, Encoding.UTF8, "text/plain");
                var res = await client.PostAsync(countEndpoint, content);
                if (!res.IsSuccessStatusCode) return 0;

                var json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number)
                    return c.GetInt32();

                return 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Normaliza dir para asc ou desc.
        /// </summary>
        private static string NormalizeDir(string? dir)
        {
            dir = (dir ?? "desc").Trim().ToLowerInvariant();
            return (dir == "asc") ? "asc" : "desc";
        }

        /// <summary>
        /// Obtém token OAuth do Twitch.
        /// </summary>
        private async Task<string> GetTwitchToken(string clientId, string clientSecret)
        {
            var client = _http.CreateClient();
            var url =
                "https://id.twitch.tv/oauth2/token" +
                $"?client_id={clientId}" +
                $"&client_secret={clientSecret}" +
                "&grant_type=client_credentials";

            var r = await client.PostAsync(url, null);
            var text = await r.Content.ReadAsStringAsync();

            if (!r.IsSuccessStatusCode)
                throw new Exception($"Twitch token failed ({r.StatusCode}): {text}");

            using var jsonDoc = JsonDocument.Parse(text);
            return jsonDoc.RootElement.GetProperty("access_token").GetString()!;
        }
    }
}