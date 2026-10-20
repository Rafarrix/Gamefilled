using Gamefilled.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Gamefilled.Pages.api
{
    public class IgdbSearchModel : PageModel
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<IgdbSearchModel> _logger;
        private readonly IHttpClientFactory _http;
        private readonly IgdbTokenProvider _tokenProvider;

        public IgdbSearchModel(IConfiguration cfg, ILogger<IgdbSearchModel> logger, IHttpClientFactory http, IgdbTokenProvider tokenProvider)
        {
            _cfg = cfg;
            _logger = logger;
            _http = http;
            _tokenProvider = tokenProvider;
        }

        public async Task<IActionResult> OnGetAsync(string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Trim().Length > 100)
                return new JsonResult(new { error = "Search term must have 1-100 characters." }) { StatusCode = 400 };

            var clientId = _cfg["IGDB:ClientId"];
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return new JsonResult(new
                {
                    error = "Game search temporarily unavailable."
                })
                { StatusCode = 503 };
            }

            try
            {
                var token = await _tokenProvider.GetAccessTokenAsync();

                var client = _http.CreateClient();
                client.DefaultRequestHeaders.Remove("Client-ID");
                client.DefaultRequestHeaders.Add("Client-ID", clientId);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var safeTerm = EscapeIgdbString(term.Trim());

                // IMPORTANTE:
                // - adicionámos platforms.name
                // - mantemos category, follows, total_rating_count
                // - limit 25 como já tinhas
                var query = $@"
fields
    id,
    name,
    slug,
    cover.image_id,
    first_release_date,
    category,
    parent_game,
    version_parent,
    follows,
    total_rating_count,
    platforms.name;
search ""{safeTerm}"";
limit 25;
";

                var content = new StringContent(query, Encoding.UTF8, "text/plain");
                var response = await client.PostAsync("https://api.igdb.com/v4/games", content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("IGDB /games failed with HTTP {Status}", (int)response.StatusCode);

                    return new JsonResult(new
                    {
                        error = "Game search temporarily unavailable."
                    })
                    { StatusCode = 502 };
                }

                return Content(responseText, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IGDB search");
                return new JsonResult(new { error = "Game search temporarily unavailable." }) { StatusCode = 502 };
            }
        }

        private static string EscapeIgdbString(string s)
        {
            return (s ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

    }
}
