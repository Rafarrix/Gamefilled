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
public sealed class ForgotPasswordModel(
    UserManager<GamefilledUser> users,
    ITransactionalEmailSender mail,
    AccountLinkFactory links,
    ILogger<ForgotPasswordModel> logger) : PageModel
{
    [BindProperty, Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    public bool RequestAccepted { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await users.FindByEmailAsync(Email.Trim());

        // Never disclose whether an account exists or is verified.
        if (user is not null && await users.IsEmailConfirmedAsync(user))
        {
            try
            {
                var token = await users.GeneratePasswordResetTokenAsync(user);
                var url = links.ResetPasswordUrl(user.Id, token);
                await mail.SendAsync(
                    user.Email!,
                    "Reset your Gamefilled password",
                    $"If you requested a password reset, open this link: {url}\nIf not, you can ignore this message.",
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
            {
                logger.LogWarning("Password reset email delivery failed.");
            }
        }

        RequestAccepted = true;
        return Page();
    }
}
