using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Gamefilled.Pages
{
    public class SearchModel : PageModel
    {
        private readonly IHttpClientFactory _http;
        private readonly ILogger<SearchModel> _logger;

        public SearchModel(IHttpClientFactory http, ILogger<SearchModel> logger)
        {
            _http = http;
            _logger = logger;
        }

        [BindProperty(SupportsGet = true)]
        public string? Query { get; set; }

        public string? Error { get; set; }

        public List<SearchGame> Results { get; set; } = new();

        public async Task OnGetAsync()
        {
            var term = (Query ?? "").Trim();

            if (term.Length < 2) return;

            try
            {
                var client = _http.CreateClient();
                var url = $"/api/igdbsearch?term={Uri.EscapeDataString(term)}";

                if (client.BaseAddress == null)
                {
                    var baseUrl = $"{Request.Scheme}://{Request.Host}";
                    url = $"{baseUrl}/api/igdbsearch?term={Uri.EscapeDataString(term)}";
                }

                var res = await client.GetAsync(url);
                var json = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Search -> /api/igdbsearch failed {Status}. Body: {Body}", res.StatusCode, json);
                    Error = "Search is temporarily unavailable. Please try again.";
                    return;
                }

                Results = ParseApiResponse(json);

                Results = Results
                    .OrderByDescending(g => g.Category == 0)
                    .ThenByDescending(g => g.Follows)
                    .ThenByDescending(g => g.TotalRatingCount)
                    .ThenBy(g => g.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in /search");
                Error = "Search is temporarily unavailable. Please try again.";
            }
        }

        private static List<SearchGame> ParseApiResponse(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("results", out var resultsEl))
            {
                if (resultsEl.ValueKind == JsonValueKind.Array)
                    return ParseGamesArray(resultsEl);
            }

            if (root.ValueKind == JsonValueKind.Array)
                return ParseGamesArray(root);

            return new List<SearchGame>();
        }

        private static List<SearchGame> ParseGamesArray(JsonElement arr)
        {
            var list = new List<SearchGame>();

            foreach (var el in arr.EnumerateArray())
            {
                if (!el.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.Number)
                    continue;

                int id = idEl.GetInt32();
                if (id <= 0) continue;

                var name = el.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var slug = el.TryGetProperty("slug", out var s) ? s.GetString() : null;

                int? category = null;
                if (el.TryGetProperty("category", out var c) && c.ValueKind == JsonValueKind.Number)
                    category = c.GetInt32();

                int follows = 0;
                if (el.TryGetProperty("follows", out var f) && f.ValueKind == JsonValueKind.Number)
                    follows = f.GetInt32();

                int totalRatingCount = 0;
                if (el.TryGetProperty("total_rating_count", out var trc) && trc.ValueKind == JsonValueKind.Number)
                    totalRatingCount = trc.GetInt32();

                int? year = null;
                if (el.TryGetProperty("first_release_date", out var frd) && frd.ValueKind == JsonValueKind.Number)
                {
                    var unix = frd.GetInt64();
                    if (unix > 0)
                        year = DateTimeOffset.FromUnixTimeSeconds(unix).Year;
                }

                string? coverImageId = null;
                if (el.TryGetProperty("cover", out var coverEl) && coverEl.ValueKind == JsonValueKind.Object)
                {
                    if (coverEl.TryGetProperty("image_id", out var imgIdEl) && imgIdEl.ValueKind == JsonValueKind.String)
                        coverImageId = imgIdEl.GetString();
                }

                var platforms = new List<string>();
                if (el.TryGetProperty("platforms", out var platsEl) && platsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in platsEl.EnumerateArray())
                    {
                        if (p.ValueKind == JsonValueKind.Object &&
                            p.TryGetProperty("name", out var pn) &&
                            pn.ValueKind == JsonValueKind.String)
                        {
                            var pnStr = pn.GetString()?.Trim();
                            if (!string.IsNullOrWhiteSpace(pnStr) && !platforms.Contains(pnStr))
                                platforms.Add(pnStr);
                        }
                    }
                }

                list.Add(new SearchGame
                {
                    Id = id,
                    Name = name!,
                    Slug = slug ?? "",
                    Year = year,
                    Category = category,
                    CategoryLabel = CategoryToLabel(category),
                    Follows = follows,
                    TotalRatingCount = totalRatingCount,
                    CoverImageId = coverImageId,
                    Platforms = platforms
                });
            }

            return list;
        }

        private static string CategoryToLabel(int? cat)
        {
            return cat switch
            {
                0 => "Main Game",
                1 => "DLC / Addon",
                2 => "Expansion",
                3 => "Bundle",
                4 => "Standalone Expansion",
                5 => "Mod",
                6 => "Episode",
                7 => "Season",
                8 => "Remake",
                9 => "Remaster",
                10 => "Expanded Game",
                11 => "Port",
                12 => "Fork",
                13 => "Pack",
                _ => ""
            };
        }

        public class SearchGame
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Slug { get; set; } = "";
            public int? Year { get; set; }

            public int? Category { get; set; }
            public string CategoryLabel { get; set; } = "";

            public int Follows { get; set; }
            public int TotalRatingCount { get; set; }

            public string? CoverImageId { get; set; }
            public List<string> Platforms { get; set; } = new();
        }
    }
}
