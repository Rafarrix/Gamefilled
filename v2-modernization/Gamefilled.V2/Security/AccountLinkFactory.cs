using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Gamefilled.V2.Security;

/// <summary>
/// Security-action URLs use an explicit allowed HTTPS origin, never the
/// untrusted incoming Host or X-Forwarded-Host headers.
/// </summary>
public sealed class AccountLinkFactory(IConfiguration configuration)
{
    public string ConfirmationUrl(Guid userId, string identityToken)
        => SecurityActionUrl("confirm", userId, identityToken);

    public string ResetPasswordUrl(Guid userId, string identityToken)
        => SecurityActionUrl("reset-password", userId, identityToken);

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
        return $"{origin.GetLeftPart(UriPartial.Authority)}/account/{action}?userId={userId:D}&code={Uri.EscapeDataString(code)}";
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
