using Gamefilled.V2.Games;
using Gamefilled.V2.Library;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Gamefilled.V2.Pages.Games;

/// <summary>
/// Game details are public. Saving or editing a collection is always POST,
/// always CSRF-protected, and scoped to the signed-in Identity principal.
/// </summary>
[EnableRateLimiting("library-write")]
public sealed class GameModel(
    IGameDetailCatalog catalog,
    GameLibraryService library,
    ILogger<GameModel> logger) : PageModel
{
    public GameDetailInfo? Game { get; private set; }
    public GameLibraryEntry? MyEntry { get; private set; }

    [TempData]
    public string? Feedback { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken ct)
    {
        if (id <= 0)
            return NotFound();

        try
        {
            Game = await catalog.GetAsync(id, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            logger.LogWarning("IGDB game detail unavailable for numeric game ID.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (Game is null)
            return NotFound();

        if (User.Identity?.IsAuthenticated == true)
            MyEntry = await library.MyGameAsync(User, id, ct);

        Response.Headers.CacheControl = "private, no-store";
        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(
        int id, string? status, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Challenge();

        if (id <= 0 || GameLibraryStatus.Normalize(status) is null)
            return BadRequest();

        await library.SaveStatusAsync(User, id, status, ct);
        Feedback = "Your library status has been updated.";
        return RedirectToPage("/Games/Game", new { id });
    }

    public async Task<IActionResult> OnPostReviewAsync(
        int id, int? rating, string? reviewText, bool containsSpoilers,
        CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Challenge();

        if (id <= 0 || rating is < 1 or > 10 || reviewText?.Length > 5000)
            return BadRequest();

        await library.SaveReviewAsync(User, id, rating, reviewText, containsSpoilers, ct);
        Feedback = "Your review has been saved.";
        return RedirectToPage("/Games/Game", new { id });
    }

    public async Task<IActionResult> OnPostRemoveAsync(int id, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Challenge();

        if (id <= 0)
            return BadRequest();

        await library.RemoveAsync(User, id, ct);
        Feedback = "Game removed from your library.";
        return RedirectToPage("/Games/Game", new { id });
    }
}
