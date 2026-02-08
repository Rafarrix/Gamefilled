using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Gamefilled.Pages.api
{
    [IgnoreAntiforgeryToken]
    public class IgdbDiscoverModel : PageModel
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<IgdbDiscoverModel> _logger;
        private readonly IHttpClientFactory _http;

        public IgdbDiscoverModel(IConfiguration cfg, ILogger<IgdbDiscoverModel> logger, IHttpClientFactory http)
        {
            _cfg = cfg;
            _logger = logger;
            _http = http;
        }

        public async Task<IActionResult> OnGetAsync(
            string sort = "popular",
            string dir = "desc",
            int page = 1,
            int pageSize = 36)
        {
            if (page < 1) page = 1;
            if (pageSize < 12) pageSize = 12;
            if (pageSize > 60) pageSize = 60;

            sort = (sort ?? "popular").Trim().ToLowerInvariant();
            dir = (dir ?? "desc").Trim().ToLowerInvariant() == "asc" ? "asc" : "desc";

            var clientId = _cfg["IGDB:ClientId"] ?? _cfg["Igdb:ClientId"];
            var clientSecret = _cfg["IGDB:ClientSecret"] ?? _cfg["Igdb:ClientSecret"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return new JsonResult(new { error = "IGDB credentials em falta" }) { StatusCode = 500 };

            try
            {
                var token = await GetTwitchToken(clientId, clientSecret);

                var client = _http.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Client-ID", clientId);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                if (sort == "avg-play" || sort == "avg-finish")
                    return await DiscoverByTimeToBeatAsync(client, sort, dir, page, pageSize);

                return await DiscoverByGamesAsync(client, sort, dir, page, pageSize);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no IgdbDiscover");
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        // =========================================================
        // A) /games (popular/trending/top-rated/release-date/game-title)
        // =========================================================
        private async Task<IActionResult> DiscoverByGamesAsync(HttpClient client, string sort, string dir, int page, int pageSize)
        {
            int offset = (page - 1) * pageSize;

            // ✅ Sem time_to_beat aqui (dava 400)
            var fields =
                "fields id,name,slug,cover.image_id,first_release_date," +
                "follows,hypes,total_rating,total_rating_count,category;";

            // ✅ Base mínima (não mata o catálogo)
            // (Depois voltamos a “filtrar qualidade” com cuidado)
            string where = "where cover != null & slug != null & name != null;";
            string sortLine = $"sort follows {dir};";

            if (sort == "popular")
            {
                where = "where cover != null & slug != null & name != null & follows != null & follows > 0;";
                sortLine = $"sort follows {dir};";
            }
            else if (sort == "trending")
            {
                // Trending: hypes > 0 + janela recente para melhorar qualidade sem cortar tudo
                var fiveYearsAgo = DateTimeOffset.UtcNow.AddDays(-365 * 5).ToUnixTimeSeconds();
                where = $"where cover != null & slug != null & name != null & hypes != null & hypes > 0 & first_release_date != null & first_release_date > {fiveYearsAgo};";
                sortLine = $"sort hypes {dir};";
            }
            else if (sort == "top-rated")
            {
                // filtro suave para evitar jogos “top” com poucos votos
                where = "where cover != null & slug != null & name != null & total_rating != null & total_rating_count != null & total_rating_count >= 50;";
                sortLine = $"sort total_rating {dir};";
            }
            else if (sort == "release-date")
            {
                where = "where cover != null & slug != null & name != null & first_release_date != null;";
                sortLine = $"sort first_release_date {dir};";
            }
            else if (sort == "game-title")
            {
                where = "where cover != null & slug != null & name != null;";
                sortLine = $"sort name {dir};";
            }

            string query = fields + where + sortLine + $"limit {pageSize};" + $"offset {offset};";

            var content = new StringContent(query, Encoding.UTF8, "text/plain");
            var response = await client.PostAsync("https://api.igdb.com/v4/games", content);
            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new JsonResult(new
                {
                    error = "IGDB request falhou",
                    status = (int)response.StatusCode,
                    body = responseText,
                    sentQuery = query
                })
                { StatusCode = 500 };
            }

            int totalCount = await TryGetCountAsync(client, "https://api.igdb.com/v4/games/count", where);

            using var doc = JsonDocument.Parse(responseText);
            var results = doc.RootElement.Clone();

            // Enriquecer tempos (best effort)
            var timeMap = await GetTimesForGamesAsync(client, results);

            // ✅ Debug: devolvemos a query e o where/sortLine para confirmares
            return new JsonResult(new
            {
                sort,
                dir,
                page,
                pageSize,
                totalCount,
                debug = new { where, sortLine, sentQuery = query },
                results,
                time_to_beat = timeMap
            });
        }

        // =========================================================
        // B) Avg Play / Avg Finish: /game_time_to_beats -> /games por ids
        // =========================================================
        private async Task<IActionResult> DiscoverByTimeToBeatAsync(HttpClient client, string sort, string dir, int page, int pageSize)
        {
            int offset = (page - 1) * pageSize;

            // ✅ Campo correto aqui é game_id (não "game")
            var ttbFields = "fields game_id,normally,completely;";

            string where;
            string sortLine;

            if (sort == "avg-play")
            {
                where = "where game_id != null & normally != null & normally > 0;";
                sortLine = $"sort normally {dir};";
            }
            else
            {
                where = "where game_id != null & completely != null & completely > 0;";
                sortLine = $"sort completely {dir};";
            }

            string ttbQuery = ttbFields + where + sortLine + $"limit {pageSize};" + $"offset {offset};";

            var ttbContent = new StringContent(ttbQuery, Encoding.UTF8, "text/plain");
            var ttbRes = await client.PostAsync("https://api.igdb.com/v4/game_time_to_beats", ttbContent);
            var ttbText = await ttbRes.Content.ReadAsStringAsync();

            if (!ttbRes.IsSuccessStatusCode)
            {
                return new JsonResult(new
                {
                    error = "IGDB request falhou",
                    status = (int)ttbRes.StatusCode,
                    body = ttbText,
                    sentQuery = ttbQuery
                })
                { StatusCode = 500 };
            }

            var orderedIds = new List<long>();
            var timesById = new Dictionary<long, object>();

            using (var doc = JsonDocument.Parse(ttbText))
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var row in doc.RootElement.EnumerateArray())
                    {
                        long id = row.TryGetProperty("game_id", out var idEl) && idEl.ValueKind == JsonValueKind.Number
                            ? idEl.GetInt64()
                            : 0;
                        if (id <= 0) continue;

                        int? normally = row.TryGetProperty("normally", out var nEl) && nEl.ValueKind == JsonValueKind.Number
                            ? nEl.GetInt32()
                            : (int?)null;

                        int? completely = row.TryGetProperty("completely", out var cEl) && cEl.ValueKind == JsonValueKind.Number
                            ? cEl.GetInt32()
                            : (int?)null;

                        orderedIds.Add(id);
                        timesById[id] = new { normally, completely };
                    }
                }
            }

            int totalCount = await TryGetCountAsync(client, "https://api.igdb.com/v4/game_time_to_beats/count", where);

            if (orderedIds.Count == 0)
            {
                return new JsonResult(new
                {
                    sort,
                    dir,
                    page,
                    pageSize,
                    totalCount,
                    debug = new { where, sortLine, sentQuery = ttbQuery },
                    results = Array.Empty<object>(),
                    time_to_beat = new { }
                });
            }

            var gamesArray = await FetchGamesByIdsJsonAsync(client, orderedIds);

            // Reordenar pela ordem do endpoint de tempos
            var byId = new Dictionary<long, JsonElement>();
            foreach (var g in gamesArray.EnumerateArray())
            {
                if (g.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
                {
                    var id = idEl.GetInt64();
                    byId[id] = g;
                }
            }

            var orderedGames = new List<JsonElement>();
            foreach (var id in orderedIds)
            {
                if (byId.TryGetValue(id, out var g))
                    orderedGames.Add(g);
            }

            return new JsonResult(new
            {
                sort,
                dir,
                page,
                pageSize,
                totalCount,
                debug = new { where, sortLine, sentQuery = ttbQuery },
                results = orderedGames,
                time_to_beat = timesById
            });
        }

        // =========================================================
        // Helpers
        // =========================================================
        private async Task<int> TryGetCountAsync(HttpClient client, string endpoint, string where)
        {
            try
            {
                var countContent = new StringContent(where, Encoding.UTF8, "text/plain");
                var countRes = await client.PostAsync(endpoint, countContent);
                if (!countRes.IsSuccessStatusCode) return 0;

                var countJson = await countRes.Content.ReadAsStringAsync();
                using var docCount = JsonDocument.Parse(countJson);
                if (docCount.RootElement.TryGetProperty("count", out var c) && c.ValueKind == JsonValueKind.Number)
                    return c.GetInt32();
                return 0;
            }
            catch { return 0; }
        }

        private async Task<Dictionary<long, object>> GetTimesForGamesAsync(HttpClient client, JsonElement gamesResults)
        {
            var ids = new List<long>();

            if (gamesResults.ValueKind == JsonValueKind.Array)
            {
                foreach (var g in gamesResults.EnumerateArray())
                {
                    if (g.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number)
                        ids.Add(idEl.GetInt64());
                }
            }

            if (ids.Count == 0) return new Dictionary<long, object>();

            var idList = string.Join(",", ids);

            var fields = "fields game_id,normally,completely;";
            var where = $"where game_id = ({idList});";
            var query = fields + where + "limit 500;";

            var content = new StringContent(query, Encoding.UTF8, "text/plain");
            var res = await client.PostAsync("https://api.igdb.com/v4/game_time_to_beats", content);
            var json = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) return new Dictionary<long, object>();

            var map = new Dictionary<long, object>();

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in doc.RootElement.EnumerateArray())
                {
                    long id = row.TryGetProperty("game_id", out var idEl) && idEl.ValueKind == JsonValueKind.Number
                        ? idEl.GetInt64()
                        : 0;
                    if (id <= 0) continue;

                    int? normally = row.TryGetProperty("normally", out var nEl) && nEl.ValueKind == JsonValueKind.Number
                        ? nEl.GetInt32()
                        : (int?)null;

                    int? completely = row.TryGetProperty("completely", out var cEl) && cEl.ValueKind == JsonValueKind.Number
                        ? cEl.GetInt32()
                        : (int?)null;

                    map[id] = new { normally, completely };
                }
            }

            return map;
        }

        private async Task<JsonElement> FetchGamesByIdsJsonAsync(HttpClient client, List<long> ids)
        {
            var idList = string.Join(",", ids);

            var fields =
                "fields id,name,slug,cover.image_id,first_release_date," +
                "follows,hypes,category,total_rating,total_rating_count;";

            // ✅ Sem filtro de category aqui também (senão pode matar os resultados do avg-*)
            var where = "where id = (" + idList + ") & cover != null & slug != null & name != null;";

            var query = fields + where + "limit 500;";

            var content = new StringContent(query, Encoding.UTF8, "text/plain");
            var res = await client.PostAsync("https://api.igdb.com/v4/games", content);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                using var emptyDoc = JsonDocument.Parse("[]");
                return emptyDoc.RootElement.Clone();
            }

            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
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
