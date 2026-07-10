using Gamefilled.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Gamefilled.Pages.games;

public sealed class GameModel : PageModel
{
    private readonly IgdbClient _igdbClient;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GameModel> _logger;

    public GameModel(
        IgdbClient igdbClient,
        IHttpClientFactory httpClientFactory,
        ILogger<GameModel> logger)
    {
        _igdbClient = igdbClient;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public IgdbGameDetailsDto? Game { get; private set; }
    public int? TtbNormallySeconds { get; private set; }
    public int? TtbCompletelySeconds { get; private set; }
    public int? TtbHastilySeconds { get; private set; }
    public string? BgUrl { get; private set; }
    public string? CoverUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
            return NotFound();

        try
        {
            Game = await _igdbClient.GetGameDetailsAsync(id, cancellationToken);

            if (Game is null)
                return NotFound();

            await LoadTimeToBeatAsync(id, cancellationToken);

            var backgroundImage = (Game.Artworks ?? [])
                .Concat(Game.Screenshots ?? [])
                .Where(image => !string.IsNullOrWhiteSpace(image.ImageId))
                .OrderByDescending(image =>
                    image.Width.HasValue &&
                    image.Height.HasValue &&
                    image.Width.Value >= image.Height.Value)
                .ThenByDescending(image =>
                    (long)(image.Width ?? 0) * (image.Height ?? 0))
                .FirstOrDefault();

            var backgroundImageId = backgroundImage?.ImageId ?? Game.Cover?.ImageId;

            BgUrl = BuildIgdbImage(backgroundImageId, "t_1080p_2x", "webp");
            CoverUrl = BuildIgdbImage(Game.Cover?.ImageId, "t_cover_big", "jpg");

            return Page();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to load game {GameId}.", id);
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    private async Task LoadTimeToBeatAsync(
        int gameId,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var relativeUrl = $"/api/igdbttb?id={gameId}";
            var requestUrl = client.BaseAddress is not null
                ? relativeUrl
                : $"{Request.Scheme}://{Request.Host}{relativeUrl}";

            using var response = await client.GetAsync(requestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return;

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            ParseTimeToBeat(responseBody);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(
                exception,
                "Unable to load time-to-beat data for game {GameId}.",
                gameId);
        }
    }

    private void ParseTimeToBeat(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return;

        var first = document.RootElement.EnumerateArray().FirstOrDefault();
        if (first.ValueKind != JsonValueKind.Object)
            return;

        if (first.TryGetProperty("normally", out var normally) &&
            normally.ValueKind == JsonValueKind.Number)
        {
            TtbNormallySeconds = normally.GetInt32();
        }

        if (first.TryGetProperty("completely", out var completely) &&
            completely.ValueKind == JsonValueKind.Number)
        {
            TtbCompletelySeconds = completely.GetInt32();
        }

        if (first.TryGetProperty("hastily", out var hastily) &&
            hastily.ValueKind == JsonValueKind.Number)
        {
            TtbHastilySeconds = hastily.GetInt32();
        }
    }

    private static string? BuildIgdbImage(string? imageId, string size, string extension) =>
        string.IsNullOrWhiteSpace(imageId)
            ? null
            : $"https://images.igdb.com/igdb/image/upload/{size}/{imageId}.{extension}";
}
