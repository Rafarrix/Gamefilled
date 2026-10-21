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
public sealed class RegisterModel(
    UserManager<GamefilledUser> users,
    ITransactionalEmailSender mail,
    AccountLinkFactory links,
    ILogger<RegisterModel> logger) : PageModel
{
    [BindProperty, Required, RegularExpression("^[A-Za-z0-9_]{3,30}$")]
    public string Username { get; set; } = "";

    [BindProperty, Required, EmailAddress, StringLength(254)]
    public string Email { get; set; } = "";

    [BindProperty, Required, StringLength(128, MinimumLength = 12)]
    public string Password { get; set; } = "";

    [BindProperty]
    public bool AcceptTerms { get; set; }

    public bool RequestAccepted { get; private set; }
    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!AcceptTerms)
            ModelState.AddModelError(nameof(AcceptTerms), "Please accept the terms and privacy notice.");

        if (!ModelState.IsValid)
            return Page();

        // The same response for new and existing accounts avoids public email enumeration.
        var user = new GamefilledUser
        {
            Id = Guid.NewGuid(),
            UserName = Username.Trim(),
            Email = Email.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        IdentityResult created;
        try
        {
            created = await users.CreateAsync(user, Password);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Concurrent duplicate registration may violate the database unique index.
            RequestAccepted = true;
            return Page();
        }

        if (!created.Succeeded)
        {
            RequestAccepted = true;
            return Page();
        }

        try
        {
            var token = await users.GenerateEmailConfirmationTokenAsync(user);
            var url = links.ConfirmationUrl(user.Id, token);
            await mail.SendAsync(
                user.Email!,
                "Confirm your Gamefilled account",
                $"Welcome to Gamefilled. Confirm your email using this link: {url}",
                cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            // An unverified user exists. They can use /account/resend later.
            // Never log the token, destination address or API provider response.
            logger.LogWarning("Account confirmation delivery failed; verification is still required.");
        }

        RequestAccepted = true;
        return Page();
    }
}
