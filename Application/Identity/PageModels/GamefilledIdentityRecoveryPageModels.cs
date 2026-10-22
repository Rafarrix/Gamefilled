using Gamefilled.Application.Identity;
using Gamefilled.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Gamefilled.Application.Identity.PageModels;

/// <summary>
/// Future recovery handler. Never discloses if an account exists and always
/// responds generically after a well-formed request.
/// </summary>
[EnableRateLimiting("registration-ip")]
public sealed class GamefilledIdentityForgotPasswordPageModel(
    GamefilledIdentityAccountService accounts) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    public bool RequestAccepted { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        // Bound input is capped before any user lookup or email attempt.
        if (Email.Length <= 254)
            await accounts.RequestPasswordResetAsync(Email, ct);

        RequestAccepted = true;
        return Page();
    }
}

/// <summary>
/// Future resend-confirmation handler; same response whether the address
/// exists, is already verified or is unknown.
/// </summary>
[EnableRateLimiting("registration-ip")]
public sealed class GamefilledIdentityResendConfirmationPageModel(
    GamefilledIdentityAccountService accounts) : PageModel
{
    [BindProperty]
    public string Email { get; set; } = string.Empty;

    public bool RequestAccepted { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (Email.Length <= 254)
            await accounts.ResendConfirmationAsync(Email, ct);

        RequestAccepted = true;
        return Page();
    }
}

/// <summary>
/// Staged token-reset handler. The GET may only display the reset form;
/// changing a password requires POST, valid token and antiforgery.
/// </summary>
[EnableRateLimiting("registration-ip")]
public sealed class GamefilledIdentityResetPasswordPageModel(
    GamefilledIdentityAccountService accounts) : PageModel
{
    [BindProperty]
    public Guid UserId { get; set; }

    [BindProperty]
    public string Code { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    public bool Invalid { get; private set; }
    public bool Completed { get; private set; }
    public string? Error { get; private set; }

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

        if (!GamefilledIdentityFormPolicy.IsAllowedPassword(NewPassword))
        {
            Error = "Use a strong password of 12–128 characters, including upper/lowercase, a number and a symbol.";
            return Page();
        }

        if (NewPassword != ConfirmPassword)
        {
            Error = "The passwords do not match.";
            return Page();
        }

        Completed = await accounts.ResetPasswordAsync(UserId, Code, NewPassword);
        Invalid = !Completed;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        // Users must sign in again with the newly reset password.
        return Page();
    }
}
