namespace Gamefilled.Application.Identity;

/// <summary>
/// Exact up-front input policy for the staged Identity account form.
/// Kept aligned with the configured official Identity password validator,
/// so weak passwords are not incorrectly shown as "email sent".
/// </summary>
public static class GamefilledIdentityFormPolicy
{
    public static bool IsAllowedPassword(string? password)
    {
        if (password is not { Length: >= 12 and <= 128 })
            return false;

        return password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit)
            && password.Any(c => !char.IsLetterOrDigit(c))
            && password.Distinct().Take(3).Count() == 3;
    }

    public static bool IsSafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && returnUrl.StartsWith("/", StringComparison.Ordinal)
        && !returnUrl.StartsWith("//", StringComparison.Ordinal)
        && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
        && !returnUrl.Contains('\\')
        && !returnUrl.Any(char.IsControl);
}
