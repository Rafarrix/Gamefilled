using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Gamefilled.Pages.api
{
    [IgnoreAntiforgeryToken]
    public class IgdbSearchModel : PageModel
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<IgdbSearchModel> _logger;
        private readonly IHttpClientFactory _http;

        public IgdbSearchModel(IConfiguration cfg, ILogger<IgdbSearchModel> logger, IHttpClientFactory http)
        {
            _cfg = cfg;
            _logger = logger;
            _http = http;
        }

        public async Task<IActionResult> OnGetAsync(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return new JsonResult(new { error = "Empty query." }) { StatusCode = 400 };

            var clientId = _cfg["IGDB:ClientId"];
            var clientSecret = _cfg["IGDB:ClientSecret"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            {
                return new JsonResult(new
                {
                    error = "Missing IGDB config (IGDB:ClientId / IGDB:ClientSecret)."
                })
                { StatusCode = 500 };
            }

            try
            {
                var token = await GetTwitchToken(clientId, clientSecret);

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
                    _logger.LogWarning("IGDB /games failed: {Status} | Body: {Body}", response.StatusCode, responseText);

                    return new JsonResult(new
                    {
                        error = "IGDB request failed",
                        status = (int)response.StatusCode,
                        body = responseText
                    })
                    { StatusCode = 500 };
                }

                return Content(responseText, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IGDB search");
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        private static string EscapeIgdbString(string s)
        {
            return (s ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
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