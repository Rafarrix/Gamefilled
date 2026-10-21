using System.ComponentModel.DataAnnotations;
using Gamefilled.V2.Identity;
using Gamefilled.V2.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.V2.Pages.Account;

public sealed class ResetPasswordModel(UserManager<GamefilledUser> users) : PageModel
{
    [BindProperty]
    public Guid UserId { get; set; }

    [BindProperty, Required, StringLength(8192)]
    public string Code { get; set; } = "";

    [BindProperty, Required, StringLength(128, MinimumLength = 12)]
    public string NewPassword { get; set; } = "";

    [BindProperty, Required, Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = "";

    public bool Completed { get; private set; }
    public bool Invalid { get; private set; }

    public void OnGet(Guid userId, string? code)
    {
        UserId = userId;
        Code = code ?? "";
        Invalid = userId == Guid.Empty || AccountLinkFactory.DecodeConfirmationCode(code) is null;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var identityToken = AccountLinkFactory.DecodeConfirmationCode(Code);
        if (UserId == Guid.Empty || identityToken is null)
        {
            Invalid = true;
            return Page();
        }

        var user = await users.FindByIdAsync(UserId.ToString("D"));
        if (user is null || !await users.IsEmailConfirmedAsync(user))
        {
            Invalid = true;
            return Page();
        }

        var result = await users.ResetPasswordAsync(user, identityToken, NewPassword);
        if (result.Succeeded)
        {
            // Identity already changes the security stamp during reset, but
            // rotate explicitly for defense in depth and old session revocation.
            Completed = (await users.UpdateSecurityStampAsync(user)).Succeeded;
            Invalid = !Completed;
        }
        else
        {
            Invalid = true;
        }

        return Page();
    }
}
