using System.ComponentModel.DataAnnotations;

namespace Gamefilled.Application.Security;

/// <summary>
/// Validates new-account input before any database query or password hashing.
/// Existing users are unaffected by these rules.
/// </summary>
public static class RegistrationInputPolicy
{
    public const int MinimumPasswordLength = 12;
    public const int MaximumPasswordLength = 128;

    public static bool TryValidate(
        string? username,
        string? email,
        string? password,
        string? confirmation,
        out string normalizedUsername,
        out string normalizedEmail,
        out string error)
    {
        normalizedUsername = username?.Trim() ?? string.Empty;
        normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedUsername)
            || string.IsNullOrWhiteSpace(normalizedEmail)
            || string.IsNullOrEmpty(password)
            || string.IsNullOrEmpty(confirmation))
        {
            error = "Please fill in all fields.";
            return false;
        }

        if (normalizedUsername.Length < 3 || normalizedUsername.Length > 30
            || normalizedUsername.Any(c => !(c is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '_')))
        {
            error = "Username must be 3-30 characters, using letters, numbers or underscores.";
            return false;
        }

        if (normalizedEmail.Length > 150
            || normalizedEmail.Any(char.IsWhiteSpace)
            || !new EmailAddressAttribute().IsValid(normalizedEmail))
        {
            error = "Please enter a valid email address (up to 150 characters).";
            return false;
        }

        // Never Trim() passwords: leading and trailing spaces are meaningful.
        if (password.Length < MinimumPasswordLength || password.Length > MaximumPasswordLength)
        {
            error = "Password must be 12-128 characters.";
            return false;
        }

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            error = "Passwords do not match.";
            return false;
        }

        return true;
    }
}
