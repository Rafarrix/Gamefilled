using Gamefilled.Application.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Gamefilled.Application.Identity.PageModels;

/// <summary>
/// Future Identity PageModel for the EXISTING Pages/users/Register.cshtml UI.
/// Staged outside the routable Pages folder until a single-authority cutover.
/// </summary>
[EnableRateLimiting("registration-ip")]
public sealed class GamefilledIdentityRegisterPageModel(
    GamefilledIdentityAccountService accounts) : PageModel
{
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmPassword { get; set; } = string.Empty;

    [BindProperty]
    public bool AcceptTerms { get; set; }

    public string? Error { get; private set; }

    public bool RequestAccepted { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(Username)
            || Username.Length > 30 || string.IsNullOrWhiteSpace(Email)
            || Email.Length > 254 || !GamefilledIdentityFormPolicy.IsAllowedPassword(Password))
        {
            Error = "Enter a valid username, email, and strong password.";
            return Page();
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            Error = "The passwords do not match.";
            return Page();
        }

        if (!AcceptTerms)
        {
            Error = "You must accept the Terms of Service and Privacy Policy.";
            return Page();
        }

        if (!await accounts.RegisterAsync(
            Username, Email, Password, AcceptTerms, cancellationToken))
        {
            Error = "Check the registration fields and try again.";
            return Page();
        }

        // Do not automatically sign in unverified accounts or reveal whether
        // an address already exists. UI displays the same generic message.
        RequestAccepted = true;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        return Page();
    }
}
