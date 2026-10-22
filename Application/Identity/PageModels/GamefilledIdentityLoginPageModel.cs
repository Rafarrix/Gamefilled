using Gamefilled.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Gamefilled.Application.Identity.PageModels;

/// <summary>
/// Future Identity handler for the EXISTING Pages/users/Login.cshtml layout.
/// Not discovered as a Razor Page while kept outside Pages/**.
/// The @model switch is one part of the coordinated full auth cutover.
/// </summary>
[EnableRateLimiting("login-ip")]
public sealed class GamefilledIdentityLoginPageModel(
    GamefilledIdentitySignInService signIn) : PageModel
{
    [BindProperty]
    public string EmailOrUsername { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public void OnGet()
    {
        if (!GamefilledIdentityFormPolicy.IsSafeReturnUrl(ReturnUrl))
            ReturnUrl = null;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // The original login form binds EXACTLY these three fields; no UI rewrite.
        if (!GamefilledIdentityFormPolicy.IsSafeReturnUrl(ReturnUrl))
            ReturnUrl = null;

        if (string.IsNullOrWhiteSpace(EmailOrUsername)
            || EmailOrUsername.Length > 254 || string.IsNullOrEmpty(Password)
            || Password.Length > 128)
        {
            Error = "Invalid username, email or password.";
            return Page();
        }

        var signedIn = await signIn.SignInAsync(EmailOrUsername, Password);
        if (!signedIn)
        {
            // One response for unknown, incorrect, unconfirmed, or locked users.
            Error = "Invalid username, email or password.";
            return Page();
        }

        return !string.IsNullOrWhiteSpace(ReturnUrl)
            ? LocalRedirect(ReturnUrl)
            : RedirectToPage("/Index");
    }
}
