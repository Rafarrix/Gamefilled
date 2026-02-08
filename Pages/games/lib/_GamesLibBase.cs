using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Globalization;

namespace Gamefilled.Pages.games.lib
{
    /// <summary>
    /// Base comum para /games/lib/*
    /// - Paginação: pageNumber/pageSize
    /// - Direção: dir=asc|desc (setinhas)
    /// - Sorts:
    ///   - popular: PopScore (popularity_primitives) -> games por ids
    ///   - trending: PopScore (popularity_primitives) -> games por ids
    ///   - top-rated: /games sort total_rating (filtro suave por votos)
    ///   - release-date: /games sort first_release_date
    ///   - title: /games sort name (A-Z / Z-A)
    ///   - avg-play / avg-finish: /game_time_to_beats (game_id) -> games por ids
    /// </summary>
    public abstract class _GamesLibBase : PageModel
    {
        protected readonly IConfiguration _cfg;
        protected readonly ILogger _logger;
        protected readonly IHttpClientFactory _http;

        protected _GamesLibBase(IConfiguration cfg, ILogger logger, IHttpClientFactory http)
        {
            _cfg = cfg;
            _logger = logger;
            _http = http;
        }

        // ===== Entrada (query string)
        [BindProperty(SupportsGet = true)]
        public int PageNumber { get; set; } = 1;

        [BindProperty(SupportsGet = true)]
        public int PageSize { get; set; } = 36;

        // dir=asc|desc
        [BindProperty(SupportsGet = true)]
        public string Dir { get; set; } = "desc";

        // ===== Saída para a View
        public string SortKey { get; protected set; } = "popular";
        public string PageTitle { get; protected set; } = "Popularity";

        public int TotalCount { get; protected set; }
        public int TotalPages { get; protected set; }
        public List<GameCard> Games { get; protected set; } = new();

        // Debug
        public string? LastIgdbQuery { get; protected set; }
        public int? LastIgdbStatus { get; protected set; }
        public string? LastIgdbBody { get; protected set; }

        public class GameCard
        {
            public long Id { get; set; }
            public string Name { get; set; } = "";
            public string Slug { get; set; } = "";
            public string? CoverImageId { get; set; }

            public long? FirstReleaseDate { get; set; }
            public int? Year { get; set; }

            public double? TotalRating { get; set; }
            public int? TotalRatingCount { get; set; }

            // tempos (game_time_to_beats)
            public int? AvgPlaySeconds { get; set; }      // normally
            public int? AvgFinishSeconds { get; set; }    // completely

            public string? AvgPlayHoursText => FormatSecondsToHours(AvgPlaySeconds);
            public string? AvgFinishHoursText => FormatSecondsToHours(AvgFinishSeconds);

            // Texto para mostrar por baixo (formatado)
            public string? ReleaseDateText => FormatUnixDate(FirstReleaseDate);

            // Ex: "★ 92.4" (ou null se não existir rating)
            public string? RatingText => TotalRating.HasValue ? $"★ {TotalRating.Value:0.#}" : null;

            public string? CoverUrl =>
                string.IsNullOrWhiteSpace(CoverImageId)
                    ? null
                    : $"https://images.igdb.com/igdb/image/upload/t_cover_big/{CoverImageId}.jpg";

            private static string? FormatSecondsToHours(int? seconds)
            {
                if (seconds == null || seconds <= 0) return null;
                double hours = seconds.Value / 3600.0;
                return $"{hours:0.#}h";
            }

            private static string? FormatUnixDate(long? unixSeconds)
            {
                if (unixSeconds == null || unixSeconds <= 0) return null;

                try
                {
                    // pt-PT para ficar coerente com o projeto
                    var dt = DateTimeOffset.FromUnixTimeSeconds(unixSeconds.Value).UtcDateTime;
                    var pt = CultureInfo.GetCultureInfo("pt-PT");

                    // Backloggd-like: se tiver dia/mês, mostra data completa
                    // IGDB normalmente tem timestamp completo, por isso isto funciona bem.
                    return dt.ToString("dd MMM yyyy", pt);
                }
                catch
                {
                    return null;
                }
            }
        }

        protected abstract void Configure();

        public async Task OnGetAsync()
        {
            Configure();

            if (PageNumber < 1) PageNumber = 1;
            if (PageSize < 12) PageSize = 12;
            if (PageSize > 60) PageSize = 60;

            Dir = NormalizeDir(Dir);

            var clientId = _cfg["IGDB:ClientId"] ?? _cfg["Igdb:ClientId"];
            var clientSecret = _cfg["IGDB:ClientSecret"] ?? _cfg["Igdb:ClientSecret"];

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
                var token = await GetTwitchToken(clientId, clientSecret);

                var client = _http.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Client-ID", clientId);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // 1) Avg Play / Avg Finish (endpoint próprio)
                if (SortKey == "avg-play" || SortKey == "avg-finish")
                {
                    await LoadByTimeToBeatAsync(client);
                    return;
                }

                // 2) Popular / Trending (PopScore) — estilo Backloggd
                if (SortKey == "popular" || SortKey == "trending")
                {
                    await LoadByPopScoreAsync(client, SortKey);
                    return;
                }

                // 3) Restantes: /games normal
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

        // =========================================================
        // A) /games (top-rated/release-date/title)
        // =========================================================
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
                _logger.LogWarning("IGDB /games falhou: {Status} | Body: {Body} | Query: {Query}",
                    res.StatusCode, json, query);

                Games = new();
                TotalCount = 0;
                TotalPages = 0;
                return;
            }

            using var doc = JsonDocument.Parse(json);
            Games = ParseGames(doc.RootElement);

            // Count (best effort)
            TotalCount = await TryGetCount(client, "https://api.igdb.com/v4/games/count", where);
            TotalPages = (TotalCount <= 0) ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        }

        private (string where, string sortLine) BuildWhereAndSortForGames(string sortKey, string dir)
        {
            // Filtro base: qualidade sem “matar” a lista
            var baseWhere = "where cover != null & slug != null & name != null";

            if (sortKey == "top-rated")
            {
                // Filtro suave por votos para evitar lixo no topo
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

            // fallback seguro
            return (baseWhere + ";", $"sort first_release_date {dir};");
        }

        // =========================================================
        // B) Popular/Trending via PopScore (popularity_primitives)
        // =========================================================
        private async Task LoadByPopScoreAsync(HttpClient client, string mode)
        {
            var offset = (PageNumber - 1) * PageSize;

            // A ideia: buscar IDs em popularity_primitives e depois ir aos games.
            // Como o IGDB pode ter vários “types”, fazemos:
            // 1) tentar obter popularity_type por nome (Visits/Want to Play/Playing/Played)
            // 2) se falhar, caímos num fallback com /games por rating/release-date (para nunca ficar 0)

            var typeId = await TryResolvePopularityTypeIdAsync(client, mode);
            if (typeId == null)
            {
                // fallback: nunca devolver vazio
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
                _logger.LogWarning("IGDB /popularity_primitives falhou: {Status} | Body: {Body} | Query: {Query}",
                    primRes.StatusCode, primJson, primQuery);

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
                        if (gameId > 0) orderedIds.Add(gameId);
                    }
                }
            }

            if (orderedIds.Count == 0)
            {
                await LoadFallbackPopularTrendingAsync(client, mode);
                return;
            }

            // Buscar games por IDs
            var games = await FetchGamesByIdsAsync(client, orderedIds);

            // Reordenar para ficar igual ao PopScore (Backloggd-like)
            var byId = games.ToDictionary(x => x.Id, x => x);
            var ordered = new List<GameCard>();
            foreach (var id in orderedIds)
                if (byId.TryGetValue(id, out var g))
                    ordered.Add(g);

            Games = ordered;

            // Count (best effort) — no primitives
            TotalCount = await TryGetCount(client, "https://api.igdb.com/v4/popularity_primitives/count", primWhere);
            TotalPages = (TotalCount <= 0) ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
        }

        private async Task<int?> TryResolvePopularityTypeIdAsync(HttpClient client, string mode)
        {
            // Nota: os nomes exactos podem variar. Fazemos “best effort”.
            // mode popular -> preferir "Visits" (tende a ser “popularidade” global)
            // mode trending -> preferir "Playing" ou "Want to Play" (tende a ser “tendência”)
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
                if (!t.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number) continue;
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

            // fallback: primeiro type
            return types.Count > 0 ? types[0].id : (int?)null;
        }

        private async Task LoadFallbackPopularTrendingAsync(HttpClient client, string mode)
        {
            // fallback que nunca fica 0:
            // - popular: top-rated “light” (rating_count >= 100) desc
            // - trending: release-date recente desc
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

        // =========================================================
        // C) Avg Play / Avg Finish: game_time_to_beats -> games por ids
        // =========================================================
        private async Task LoadByTimeToBeatAsync(HttpClient client)
        {
            var offset = (PageNumber - 1) * PageSize;

            // Campo correcto: game_id (não é "game") :contentReference[oaicite:2]{index=2}
            var ttbFields = "fields game_id,normally,completely;";

            string where;
            string sort;

            if (SortKey == "avg-play")
            {
                where = "where game_id != null & normally != null & normally > 0;";
                sort = $"sort normally {Dir};";
            }
            else
            {
                where = "where game_id != null & completely != null & completely > 0;";
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

            // IMPORTANTE: aqui NÃO vamos filtrar por category (isso estava a “matar” jogos e a dar lista vazia)
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

        private async Task<List<GameCard>> FetchGamesByIdsAsync(HttpClient client, List<long> ids)
        {
            var idList = string.Join(",", ids);

            var fields = "fields id,name,slug,cover.image_id,first_release_date,total_rating,total_rating_count;";
            var where =
                "where id = (" + idList + ")" +
                " & cover != null & slug != null & name != null;";

            var query = fields + where + "limit 500;";
            var content = new StringContent(query, Encoding.UTF8, "text/plain");

            var res = await client.PostAsync("https://api.igdb.com/v4/games", content);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
                return new List<GameCard>();

            using var doc = JsonDocument.Parse(json);
            return ParseGames(doc.RootElement);
        }

        // =========================================================
        // JSON + Count + Token
        // =========================================================
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
                    try { card.Year = DateTimeOffset.FromUnixTimeSeconds(card.FirstReleaseDate.Value).Year; } catch { }
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
            catch { return 0; }
        }

        private static string NormalizeDir(string? dir)
        {
            dir = (dir ?? "desc").Trim().ToLowerInvariant();
            return (dir == "asc") ? "asc" : "desc";
        }

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
