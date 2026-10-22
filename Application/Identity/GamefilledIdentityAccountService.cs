using System.Net.Mail;
using System.Text.RegularExpressions;
using Gamefilled.Application.Email;
using Gamefilled.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Application.Identity;

/// <summary>
/// Canonical Identity flows prepared in the original Gamefilled project.
/// Not registered on the running app until the coordinated no-dual-auth cutover.
/// Every public email request yields a generic accepted response.
/// </summary>
public sealed class GamefilledIdentityAccountService(
    UserManager<GamefilledAccount> users,
    ITransactionalEmailSender mail,
    GamefilledIdentityLinkFactory links,
    ILogger<GamefilledIdentityAccountService> logger)
{
    private static readonly Regex UsernamePolicy =
        new("^[A-Za-z0-9_]{3,30}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<bool> RegisterAsync(
        string? username, string? email, string? password,
        bool acceptedTerms, CancellationToken ct = default)
    {
        if (!acceptedTerms || !IsValidUsername(username)
            || !IsValidEmail(email) || !GamefilledIdentityFormPolicy.IsAllowedPassword(password))
            return false;

        var account = new GamefilledAccount
        {
            Id = Guid.NewGuid(),
            UserName = username!.Trim(),
            Email = email!.Trim(),
            EmailConfirmed = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        try
        {
            var result = await users.CreateAsync(account, password!);
            if (result.Succeeded)
                await SendConfirmationAsync(account, ct);
        }
        catch (DbUpdateException)
        {
            // Unique index races do not expose whether an account/email exists.
            return true;
        }

        // Equal response for new/duplicate emails and usernames.
        return true;
    }

    public async Task ResendConfirmationAsync(string? email, CancellationToken ct = default)
    {
        if (!IsValidEmail(email)) return;
        var account = await users.FindByEmailAsync(email!.Trim());
        if (account is not null && !await users.IsEmailConfirmedAsync(account))
            await SendConfirmationAsync(account, ct);
    }

    public async Task<bool> ConfirmEmailAsync(Guid userId, string? code)
    {
        var token = GamefilledIdentityLinkFactory.DecodeConfirmationCode(code);
        if (userId == Guid.Empty || token is null) return false;
        var account = await users.FindByIdAsync(userId.ToString("D"));
        if (account is null || await users.IsEmailConfirmedAsync(account)) return false;
        var result = await users.ConfirmEmailAsync(account, token);
        return result.Succeeded && (await users.UpdateSecurityStampAsync(account)).Succeeded;
    }

    public async Task RequestPasswordResetAsync(string? email, CancellationToken ct = default)
    {
        if (!IsValidEmail(email)) return;
        var account = await users.FindByEmailAsync(email!.Trim());
        if (account is null || !await users.IsEmailConfirmedAsync(account))
            return;

        try
        {
            var token = await users.GeneratePasswordResetTokenAsync(account);
            await mail.SendAsync(account.Email!,
                "Reset your Gamefilled password",
                "If you requested a Gamefilled password reset, open this link: "
                + links.ResetPasswordUrl(account.Id, token)
                + "\nIf you did not request it, ignore this message.",
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            // No email, token, password or provider response is logged.
            logger.LogWarning("Identity password recovery email delivery failed.");
        }
    }

    public async Task<bool> ResetPasswordAsync(Guid userId, string? code, string? newPassword)
    {
        if (userId == Guid.Empty || !IsValidPasswordInput(newPassword)) return false;
        var token = GamefilledIdentityLinkFactory.DecodeConfirmationCode(code);
        if (token is null) return false;

        var account = await users.FindByIdAsync(userId.ToString("D"));
        if (account is null || !await users.IsEmailConfirmedAsync(account)) return false;

        var result = await users.ResetPasswordAsync(account, token, newPassword!);
        return result.Succeeded && (await users.UpdateSecurityStampAsync(account)).Succeeded;
    }

    private async Task SendConfirmationAsync(GamefilledAccount account, CancellationToken ct)
    {
        try
        {
            var token = await users.GenerateEmailConfirmationTokenAsync(account);
            await mail.SendAsync(account.Email!, "Confirm your Gamefilled account",
                "Welcome to Gamefilled. Confirm your email here: "
                + links.ConfirmationUrl(account.Id, token),
                cancellationToken: ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidOperationException)
        {
            logger.LogWarning("Identity confirmation email delivery failed.");
        }
    }

    private static bool IsValidUsername(string? username) =>
        username is not null && UsernamePolicy.IsMatch(username.Trim());

    private static bool IsValidPasswordInput(string? password) =>
        password is { Length: >= 12 and <= 128 };

    private static bool IsValidEmail(string? email)
    {
        var value = email?.Trim();
        return !string.IsNullOrEmpty(value) && value.Length <= 254
            && MailAddress.TryCreate(value, out var address)
            && string.Equals(value, address.Address, StringComparison.Ordinal);
    }
}

/// <summary>
/// SignInManager issues/revokes the official Identity cookie at cutover.
/// It must never be used as an alternative to the old Session login.
/// </summary>
public sealed class GamefilledIdentitySignInService(
    UserManager<GamefilledAccount> users,
    SignInManager<GamefilledAccount> signIn)
{
    public async Task<bool> SignInAsync(string? emailOrUsername, string? password)
    {
        var identifier = emailOrUsername?.Trim();
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 254
            || string.IsNullOrEmpty(password))
            return false;

        var account = identifier.Contains('@')
            ? await users.FindByEmailAsync(identifier)
            : await users.FindByNameAsync(identifier);
        if (account is null) return false;

        // Require confirmed mail, lockout and password checks in SignInManager;
        // never construct another "userId" Session authentication ticket.
        var result = await signIn.PasswordSignInAsync(
            account, password, isPersistent: false, lockoutOnFailure: true);
        return result.Succeeded;
    }

    public Task SignOutAsync() => signIn.SignOutAsync();
}
