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
                return new JsonResult(new { error = "Query vazia" }) { StatusCode = 400 };

            // ✅ Mantém IGDB (maiúsculas) como tinhas
            var clientId = _cfg["IGDB:ClientId"];
            var clientSecret = _cfg["IGDB:ClientSecret"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return new JsonResult(new { error = "Falta config IGDB (IGDB:ClientId/IGDB:ClientSecret)" })
                { StatusCode = 500 };

            try
            {
                var token = await GetTwitchToken(clientId, clientSecret);

                var client = _http.CreateClient();
                client.DefaultRequestHeaders.Add("Client-ID", clientId);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // ✅ Query IGDB simples e válida
                // NOTA: não usamos sort aqui porque "search" já ordena por relevância
                var safeTerm = EscapeIgdbString(term.Trim());

                var query =
                    "fields id,name,slug,cover.image_id,first_release_date,category,parent_game,version_parent,follows,total_rating_count;" +
                    $"search \"{safeTerm}\";" +
                    "limit 25;";

                var content = new StringContent(query, Encoding.UTF8, "text/plain");
                var response = await client.PostAsync("https://api.igdb.com/v4/games", content);
                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("IGDB /games falhou: {Status} | Body: {Body}", response.StatusCode, responseText);
                    return new JsonResult(new { error = "IGDB request falhou", status = (int)response.StatusCode, body = responseText })
                    { StatusCode = 500 };
                }

                // ✅ devolve JSON cru (é um array)
                return Content(responseText, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no IGDB search");
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        private static string EscapeIgdbString(string s)
        {
            // evita partir a query com aspas ou backslashes
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
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
