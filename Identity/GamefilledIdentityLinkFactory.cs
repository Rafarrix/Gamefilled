using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Gamefilled.Identity;

/// <summary>
/// Security-action URLs use an explicit allowed HTTPS origin, never the
/// untrusted incoming Host or X-Forwarded-Host headers.
/// </summary>
public sealed class GamefilledIdentityLinkFactory(IConfiguration configuration)
{
    public string ConfirmationUrl(Guid userId, string identityToken)
        => SecurityActionUrl("confirm-email", userId, identityToken);

    public string ResetPasswordUrl(Guid userId, string identityToken)
        => SecurityActionUrl("reset-password", userId, identityToken);

    // New-address confirmation: Identity binds the token to BOTH user and
    // target email. Encode the address, do not allow request Host as origin.
    public string EmailChangeUrl(Guid userId, string newEmail, string identityToken)
    {
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(newEmail));
        return SecurityActionUrl("confirm-email-change", userId, identityToken)
            + "&email=" + Uri.EscapeDataString(encoded);
    }

    public static string? DecodeTargetEmail(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 512) return null;
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private string SecurityActionUrl(string action, Guid userId, string identityToken)
    {
        var rawOrigin = configuration["Gamefilled:PublicOrigin"];
        if (!Uri.TryCreate(rawOrigin, UriKind.Absolute, out var origin)
            || origin.Scheme != Uri.UriSchemeHttps
            || !string.IsNullOrEmpty(origin.UserInfo)
            || origin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(origin.Query)
            || !string.IsNullOrEmpty(origin.Fragment)
            || !(origin.Host.Equals("gamefilled.pt", StringComparison.OrdinalIgnoreCase)
                || origin.Host.EndsWith(".gamefilled.pt", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("An approved HTTPS Gamefilled:PublicOrigin is required.");
        }

        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(identityToken));
        return $"{origin.GetLeftPart(UriPartial.Authority)}/users/{action}?userId={userId:D}&code={Uri.EscapeDataString(code)}";
    }

    public static string? DecodeConfirmationCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 8192)
            return null;
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
