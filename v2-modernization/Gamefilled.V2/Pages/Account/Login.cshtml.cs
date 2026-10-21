using System.ComponentModel.DataAnnotations;
using Gamefilled.V2.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Gamefilled.V2.Pages.Account;

[EnableRateLimiting("account-forms")]
public sealed class LoginModel(
    UserManager<GamefilledUser> users,
    SignInManager<GamefilledUser> signIn) : PageModel
{
    [BindProperty, Required, EmailAddress]
    public string Email { get; set; } = "";

    [BindProperty, Required]
    public string Password { get; set; } = "";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await users.FindByEmailAsync(Email.Trim());
        if (user is not null)
        {
            var result = await signIn.PasswordSignInAsync(
                user, Password, isPersistent: false, lockoutOnFailure: true);

            if (result.Succeeded)
                return Redirect("/identity/me");
        }

        // Same error for unknown, unverified, locked and incorrect accounts.
        ModelState.AddModelError("", "Unable to sign in with these credentials.");
        return Page();
    }
}
