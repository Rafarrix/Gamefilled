using Gamefilled.V2.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.V2.Pages.Account;

[Authorize]
public sealed class LogoutModel(SignInManager<GamefilledUser> signIn) : PageModel
{
    public void OnGet() { }
    public async Task<IActionResult> OnPostAsync()
    {
        await signIn.SignOutAsync();
        return Redirect("/account/login");
    }
}
