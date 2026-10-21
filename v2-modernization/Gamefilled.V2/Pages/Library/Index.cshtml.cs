using Gamefilled.V2.Library;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Gamefilled.V2.Pages.Library;

/// <summary>
/// Private library only. Public profiles will get an explicit visibility
/// policy later; this page never trusts a user ID from the request.
/// </summary>
[Authorize]
[EnableRateLimiting("library-write")]
public sealed record LibraryGameCard(GameLibraryEntry Entry, LibraryGameMetadata? Metadata)
{
    public string Name => Metadata?.Name ?? $"Game {Entry.IgdbGameId}";
    public string? CoverUrl => Metadata?.CoverUrl;
    public int? ReleaseYear => Metadata?.ReleaseYear;
}

public sealed class IndexModel(GameLibraryService library, ILibraryGameCatalog catalog) : PageModel
{
    public const string All = "all";

    public string Section { get; private set; } = All;
    public IReadOnlyList<LibraryGameCard> Entries { get; private set; } = [];
    public int PageNumber { get; private set; } = 1;
    public int TotalPages { get; private set; } = 1;
    public int TotalItems { get; private set; }
    public IReadOnlyDictionary<string, int> Counts { get; private set; }
        = new Dictionary<string, int>();

    [TempData]
    public string? Feedback { get; set; }

    public async Task<IActionResult> OnGetAsync(
        string? section, int page = 1, CancellationToken ct = default)
    {
        var normalized = NormalizeSection(section);
        if (normalized is null)
            return NotFound();

        Section = normalized;
        var all = await library.MyGamesAsync(User, ct);
        Counts = all.GroupBy(x => x.Status)
            .ToDictionary(x => x.Key, x => x.Count());
        var matching = normalized == All
            ? all
            : all.Where(x => x.Status == normalized).ToList();
        TotalItems = matching.Count;
        TotalPages = Math.Max(1, (int)Math.Ceiling(TotalItems / 24d));
        PageNumber = Math.Clamp(page, 1, TotalPages);
        var selected = matching.Skip((PageNumber - 1) * 24).Take(24).ToList();
        var names = await catalog.GetByIdsAsync(selected.Select(x => x.IgdbGameId), ct);
        Entries = selected.Select(x => new LibraryGameCard(
            x, names.GetValueOrDefault(x.IgdbGameId))).ToList();

        Response.Headers.CacheControl = "private, no-store";
        return Page();
    }

    public async Task<IActionResult> OnPostStatusAsync(
        int gameId, string? status, string? section, CancellationToken ct)
    {
        if (gameId <= 0 || GameLibraryStatus.Normalize(status) is null)
            return BadRequest();

        await library.SaveStatusAsync(User, gameId, status, ct);
        Feedback = "Library status updated.";
        return RedirectToPage("/Library/Index", new { section = SafeSection(section) });
    }

    public async Task<IActionResult> OnPostReviewAsync(
        int gameId, int? rating, string? reviewText, bool containsSpoilers,
        string? section, CancellationToken ct)
    {
        if (gameId <= 0 || rating is < 1 or > 10 || reviewText?.Length > 5000)
            return BadRequest();

        await library.SaveReviewAsync(User, gameId, rating, reviewText, containsSpoilers, ct);
        Feedback = "Review saved.";
        return RedirectToPage("/Library/Index", new { section = SafeSection(section) });
    }

    public async Task<IActionResult> OnPostRemoveAsync(
        int gameId, string? section, CancellationToken ct)
    {
        if (gameId <= 0)
            return BadRequest();

        await library.RemoveAsync(User, gameId, ct);
        Feedback = "Game removed from your library.";
        return RedirectToPage("/Library/Index", new { section = SafeSection(section) });
    }

    private static string SafeSection(string? section) =>
        NormalizeSection(section) ?? All;

    private static string? NormalizeSection(string? section)
    {
        var candidate = section?.Trim().ToLowerInvariant();
        return candidate is null or "" or All
            ? All
            : GameLibraryStatus.Valid.Contains(candidate) ? candidate : null;
    }
}
