using System.Net.Mail;
using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.Settings;

public class AccountModel : PageModel
{
    private const int BcryptWorkFactor = 11;
    private readonly AppDbContext _db;

    public AccountModel(AppDbContext db)
    {
        _db = db;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string EmailCurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    public string PasswordCurrentPassword { get; set; } = string.Empty;

    [BindProperty]
    public string NewPassword { get; set; } = string.Empty;

    [BindProperty]
    public string ConfirmNewPassword { get; set; } = string.Empty;

    public string Username { get; private set; } = string.Empty;
    public string Role { get; private set; } = "User";
    public DateTime CreatedAt { get; private set; }
    public string? EmailSuccess { get; private set; }
    public string? EmailError { get; private set; }
    public string? PasswordSuccess { get; private set; }
    public string? PasswordError { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user == null)
            return RedirectToPage("/Users/Login", new { returnUrl = "/Settings/Account" });

        Populate(user);
        return Page();
    }

    public async Task<IActionResult> OnPostEmailAsync(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user == null)
            return RedirectToPage("/Users/Login", new { returnUrl = "/Settings/Account" });

        Populate(user);

        var email = Email.Trim().ToLowerInvariant();
        if (!IsValidEmail(email))
        {
            EmailError = "Enter a valid email address.";
            return Page();
        }

        if (!VerifyPassword(EmailCurrentPassword, user.PasswordHash))
        {
            EmailError = "The current password is incorrect.";
            return Page();
        }

        var emailTaken = await _db.Users
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id != user.Id && candidate.Email == email, cancellationToken);

        if (emailTaken)
        {
            EmailError = "That email address is already in use.";
            return Page();
        }

        if (string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            EmailError = "This is already your current email address.";
            return Page();
        }

        user.Email = email;
        await _db.SaveChangesAsync(cancellationToken);

        Populate(user);
        EmailCurrentPassword = string.Empty;
        EmailSuccess = "Email address updated.";
        return Page();
    }

    public async Task<IActionResult> OnPostPasswordAsync(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync(cancellationToken);
        if (user == null)
            return RedirectToPage("/Users/Login", new { returnUrl = "/Settings/Account" });

        Populate(user);

        if (!VerifyPassword(PasswordCurrentPassword, user.PasswordHash))
        {
            PasswordError = "The current password is incorrect.";
            return Page();
        }

        if (NewPassword.Length < 8)
        {
            PasswordError = "The new password must contain at least 8 characters.";
            return Page();
        }

        if (!NewPassword.Any(char.IsLetter) || !NewPassword.Any(char.IsDigit))
        {
            PasswordError = "Use at least one letter and one number.";
            return Page();
        }

        if (!string.Equals(NewPassword, ConfirmNewPassword, StringComparison.Ordinal))
        {
            PasswordError = "The new passwords do not match.";
            return Page();
        }

        if (VerifyPassword(NewPassword, user.PasswordHash))
        {
            PasswordError = "Choose a password different from the current one.";
            return Page();
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword, workFactor: BcryptWorkFactor);
        await _db.SaveChangesAsync(cancellationToken);

        PasswordCurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmNewPassword = string.Empty;
        PasswordSuccess = "Password updated successfully.";
        return Page();
    }

    private async Task<User?> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = HttpContext.Session.GetInt32("userId");
        if (!userId.HasValue)
            return null;

        return await _db.Users.FirstOrDefaultAsync(user => user.Id == userId.Value, cancellationToken);
    }

    private void Populate(User user)
    {
        Username = user.Username ?? $"user{user.Id}";
        Role = user.Role ?? "User";
        CreatedAt = user.CreatedAt;
        Email = user.Email ?? string.Empty;
    }

    private static bool VerifyPassword(string password, string? passwordHash) =>
        !string.IsNullOrEmpty(password) &&
        !string.IsNullOrWhiteSpace(passwordHash) &&
        BCrypt.Net.BCrypt.Verify(password, passwordHash);

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
            return false;

        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}