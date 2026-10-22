using Gamefilled.Application.Identity;
using Gamefilled.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Gamefilled.Application.Identity.PageModels;

/// <summary>
/// Prepared Identity confirmation handler for the future branded
/// /users/confirm-email page. Verification changes state only via POST.
/// </summary>
[EnableRateLimiting("registration-ip")]
public sealed class GamefilledIdentityConfirmEmailPageModel(
    GamefilledIdentityAccountService accounts) : PageModel
{
    [BindProperty]
    public Guid UserId { get; set; }

    [BindProperty]
    public string Code { get; set; } = string.Empty;

    public bool Invalid { get; private set; }
    public bool Completed { get; private set; }

    public void OnGet(Guid userId, string? code)
    {
        UserId = userId;
        Code = code ?? string.Empty;
        Invalid = userId == Guid.Empty
            || GamefilledIdentityLinkFactory.DecodeConfirmationCode(Code) is null;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (UserId == Guid.Empty
            || GamefilledIdentityLinkFactory.DecodeConfirmationCode(Code) is null)
        {
            Invalid = true;
            return Page();
        }

        Completed = await accounts.ConfirmEmailAsync(UserId, Code);
        Invalid = !Completed;
        // Confirming must not automatically authenticate the account.
        return Page();
    }
}
