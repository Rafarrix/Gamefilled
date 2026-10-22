using System.ComponentModel.DataAnnotations;
using Gamefilled.V2.Data;
using Gamefilled.V2.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.V2.Pages.Profile;

/// <summary>
/// The private Identity-owned profile foundation. No user ID is accepted in
/// the URL or POST; public profiles require an explicit privacy design first.
/// </summary>
[Authorize]
[EnableRateLimiting("profile-write")]
public sealed class IndexModel(
    UserManager<GamefilledUser> users,
    GamefilledIdentityDbContext database) : PageModel
{
    [BindProperty]
    [StringLength(100)]
    public string? DisplayName { get; set; }

    public string Username { get; private set; } = "";
    public DateTime MemberSinceUtc { get; private set; }
    public IReadOnlyDictionary<string, int> StatusCounts { get; private set; }
        = new Dictionary<string, int>();
    public int TotalGames => StatusCounts.Values.Sum();

    [TempData]
    public string? Feedback { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var account = await users.GetUserAsync(User);
        if (account is null)
            return Challenge();

        DisplayName = account.DisplayName;
        await LoadOwnSummaryAsync(account, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var account = await users.GetUserAsync(User);
        if (account is null)
            return Challenge();

        if (!ModelState.IsValid)
        {
            await LoadOwnSummaryAsync(account, ct);
            return Page();
        }

        var proposed = DisplayName?.Trim();
        account.DisplayName = string.IsNullOrWhiteSpace(proposed) ? null : proposed;
        var result = await users.UpdateAsync(account);
        if (!result.Succeeded)
        {
            // Never leak account identifiers or raw provider errors.
            ModelState.AddModelError(nameof(DisplayName), "Could not update the profile.");
            await LoadOwnSummaryAsync(account, ct);
            return Page();
        }

        Feedback = "Profile updated.";
        return RedirectToPage("/Profile/Index");
    }

    private async Task LoadOwnSummaryAsync(GamefilledUser account, CancellationToken ct)
    {
        Username = account.UserName ?? "Player";
        MemberSinceUtc = account.CreatedAtUtc;

        var counts = await database.GameLibraryEntries.AsNoTracking()
            .Where(entry => entry.UserId == account.Id)
            .GroupBy(entry => entry.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(ct);

        StatusCounts = counts.ToDictionary(x => x.Status, x => x.Count);
        Response.Headers.CacheControl = "private, no-store";
    }
}
