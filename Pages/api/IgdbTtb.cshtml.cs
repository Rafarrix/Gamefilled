using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Gamefilled.Pages.api
{
    /// <summary>
    /// Endpoint interno responsável por obter o time-to-beat de um jogo.
    ///
    /// O que este ficheiro faz:
    /// - recebe o ID do jogo
    /// - autentica-se via Twitch
    /// - chama o endpoint game_time_to_beats da IGDB
    /// - devolve a resposta em JSON
    ///
    /// Importância:
    /// Permite separar a lógica dos tempos médios da lógica principal do detalhe de jogo.
    /// </summary>
    [IgnoreAntiforgeryToken]
    public class IgdbTtbModel : PageModel
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<IgdbTtbModel> _logger;
        private readonly IHttpClientFactory _http;

        public IgdbTtbModel(IConfiguration cfg, ILogger<IgdbTtbModel> logger, IHttpClientFactory http)
        {
            _cfg = cfg;
            _logger = logger;
            _http = http;
        }

        /// <summary>
        /// Handler GET do endpoint de time-to-beat.
        /// </summary>
        public async Task<IActionResult> OnGetAsync(int id)
        {
            // Validação do id.
            if (id <= 0)
                return new JsonResult(new { error = "id inválido" }) { StatusCode = 400 };

            // Lê credenciais da configuração.
            var clientId = _cfg["IGDB:ClientId"];
            var clientSecret = _cfg["IGDB:ClientSecret"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return new JsonResult(new { error = "Falta config IGDB (IGDB:ClientId/IGDB:ClientSecret)" })
                { StatusCode = 500 };

            try
            {
                // =========================================================
                // 1) TOKEN TWITCH
                // =========================================================
                var token = await GetTwitchToken(clientId, clientSecret);

                // =========================================================
                // 2) PREPARAÇÃO DO CLIENTE HTTP
                // =========================================================
                var client = _http.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("Client-ID", clientId);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // =========================================================
                // 3) QUERY DOS TEMPOS MÉDIOS
                // =========================================================
                // A IGDB guarda estes valores normalmente em segundos.
                var query = $@"
fields game_id,hastily,normally,completely;
where game_id = {id};
limit 1;
";

                var content = new StringContent(query, Encoding.UTF8, "text/plain");

                // Pedido ao endpoint da IGDB.
                var res = await client.PostAsync("https://api.igdb.com/v4/game_time_to_beats", content);
                var json = await res.Content.ReadAsStringAsync();

                // Tratamento de erro.
                if (!res.IsSuccessStatusCode)
                {
                    _logger.LogWarning("IGDB /game_time_to_beats falhou: {Status} | Body: {Body}", res.StatusCode, json);
                    return new JsonResult(new { error = "IGDB request falhou", status = (int)res.StatusCode, body = json })
                    { StatusCode = 500 };
                }

                // Se correu bem, devolve o JSON.
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no IgdbTtb");
                return new JsonResult(new { error = ex.Message }) { StatusCode = 500 };
            }
        }

        /// <summary>
        /// Pede token OAuth ao Twitch para autenticar pedidos à IGDB.
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