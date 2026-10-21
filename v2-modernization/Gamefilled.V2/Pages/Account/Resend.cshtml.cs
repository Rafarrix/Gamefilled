using System.ComponentModel.DataAnnotations;
using Gamefilled.Application.Email;
using Gamefilled.V2.Identity;
using Gamefilled.V2.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace Gamefilled.V2.Pages.Account;

[EnableRateLimiting("email-send")]
public sealed class ResendModel(
    UserManager<GamefilledUser> users,
    ITransactionalEmailSender mail,
    AccountLinkFactory links,
    ILogger<ResendModel> logger) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    public bool RequestAccepted { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid) return Page();

        var user = await users.FindByEmailAsync(Email.Trim());
        if (user is not null && !await users.IsEmailConfirmedAsync(user))
        {
            try
            {
                var token = await users.GenerateEmailConfirmationTokenAsync(user);
                await mail.SendAsync(user.Email!, "Confirm your Gamefilled account",
                    $"Confirm your email: {links.ConfirmationUrl(user.Id, token)}",
                    cancellationToken: ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
            {
                logger.LogWarning("Confirmation resend delivery failed.");
            }
        }
        RequestAccepted = true;
        return Page();
    }
}
