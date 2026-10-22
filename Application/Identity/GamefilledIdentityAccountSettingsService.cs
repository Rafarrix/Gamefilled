using System.Net.Mail;
using System.Security.Claims;
using Gamefilled.Application.Email;
using Gamefilled.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Application.Identity;

/// <summary>
/// Stage secure account settings for the canonical Gamefilled account page.
/// Not registered or exposed while the old session handlers are active.
/// </summary>
public sealed class GamefilledIdentityAccountSettingsService(
    UserManager<GamefilledAccount> users,
    ITransactionalEmailSender mail,
    GamefilledIdentityLinkFactory links,
    ILogger<GamefilledIdentityAccountSettingsService> logger)
{
    public async Task<bool> ChangeMyPasswordAsync(
        ClaimsPrincipal viewer, string? currentPassword, string? newPassword)
    {
        var owner = GamefilledViewer.RequireUserId(viewer);
        if (string.IsNullOrEmpty(currentPassword)
            || !GamefilledIdentityFormPolicy.IsAllowedPassword(newPassword)
            || string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
            return false;

        var account = await users.FindByIdAsync(owner.ToString("D"));
        if (account is null) return false;

        // Identity verifies the old password, hashes the new one and changes
        // the security stamp (invalidating previously issued auth cookies).
        var result = await users.ChangePasswordAsync(
            account, currentPassword, newPassword!);
        return result.Succeeded;
    }

    public async Task<bool> RequestEmailChangeAsync(
        ClaimsPrincipal viewer, string? currentPassword, string? newEmail,
        CancellationToken ct = default)
    {
        var owner = GamefilledViewer.RequireUserId(viewer);
        var candidate = newEmail?.Trim();
        if (string.IsNullOrEmpty(currentPassword) || !ValidEmail(candidate))
            return false;

        var account = await users.FindByIdAsync(owner.ToString("D"));
        if (account is null || !await users.CheckPasswordAsync(account, currentPassword))
            return false;

        if (string.Equals(account.Email, candidate, StringComparison.OrdinalIgnoreCase))
            return true;

        // Only send to the proposed NEW address. Never update the SQL email
        // in this method before confirming control of the new inbox.
        var alreadyExists = await users.FindByEmailAsync(candidate!);
        if (alreadyExists is not null) return true; // generic response

        try
        {
            var token = await users.GenerateChangeEmailTokenAsync(account, candidate!);
            var link = links.EmailChangeUrl(account.Id, candidate!, token);
            await mail.SendAsync(candidate!,
                "Confirm your new Gamefilled email address",
                "If you requested to change your Gamefilled email, open this link: "
                + link + "\nIf you did not request it, ignore this message.",
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            logger.LogWarning("Identity email change confirmation could not be delivered.");
        }

        return true;
    }

    public async Task<bool> ConfirmEmailChangeAsync(
        Guid accountId, string? targetEmailEncoded, string? confirmationCode)
    {
        var email = GamefilledIdentityLinkFactory.DecodeTargetEmail(targetEmailEncoded);
        var token = GamefilledIdentityLinkFactory.DecodeConfirmationCode(confirmationCode);
        if (accountId == Guid.Empty || !ValidEmail(email) || token is null)
            return false;

        var account = await users.FindByIdAsync(accountId.ToString("D"));
        if (account is null) return false;

        // Identity validates token against the account, its security stamp,
        // AND the exact proposed target address before updating account email.
        IdentityResult changed;
        try
        {
            changed = await users.ChangeEmailAsync(account, email!, token);
        }
        catch (DbUpdateException)
        {
            // Another user may have registered this address after the email
            // was sent. The unique SQL index wins the race safely.
            return false;
        }
        if (!changed.Succeeded) return false;

        // Also update username only if it had been the old email? We preserve
        // Gamefilled's distinct public username by design.
        return (await users.UpdateSecurityStampAsync(account)).Succeeded;
    }

    private static bool ValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 254
        && MailAddress.TryCreate(email, out var parsed)
        && string.Equals(parsed.Address, email, StringComparison.Ordinal);
}
