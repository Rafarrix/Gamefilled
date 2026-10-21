using Gamefilled.V2.Identity;
using Gamefilled.V2.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.V2.Pages.Account;

public sealed class ConfirmModel(UserManager<GamefilledUser> users) : PageModel
{
    [BindProperty]
    public Guid UserId { get; set; }

    [BindProperty]
    public string Code { get; set; } = "";

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
        var token = AccountLinkFactory.DecodeConfirmationCode(Code);
        if (UserId == Guid.Empty || token is null)
        {
            Invalid = true;
            return Page();
        }

        var user = await users.FindByIdAsync(UserId.ToString("D"));
        if (user is null)
        {
            Invalid = true;
            return Page();
        }

        var result = await users.ConfirmEmailAsync(user, token);
        Completed = result.Succeeded;
        Invalid = !Completed;
        // Confirmation does not automatically log the user in.
        return Page();
    }
}
