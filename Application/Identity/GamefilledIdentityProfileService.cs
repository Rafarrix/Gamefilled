using System.Security.Claims;
using Gamefilled.Identity;
using Microsoft.AspNetCore.Identity;

namespace Gamefilled.Application.Identity;

public sealed record GamefilledAccountProfile(
    Guid Id, string Username, string DisplayName, string? Bio,
    string? AvatarUrl, string? BannerUrl, DateTime CreatedAtUtc,
    DateTime? LastSeenAtUtc, bool IsPrivate);

/// <summary>
/// Replacement for legacy header/profile session reads. Call from the same
/// canonical Razor layout after the coordinated Identity cutover.
/// The caller can ONLY read or update the currently authenticated account.
/// </summary>
public sealed class GamefilledIdentityProfileService(UserManager<GamefilledAccount> users)
{
    public async Task<GamefilledAccountProfile?> MyProfileAsync(ClaimsPrincipal viewer)
    {
        var id = GamefilledViewer.RequireUserId(viewer);
        var account = await users.FindByIdAsync(id.ToString("D"));
        return account is null ? null : new GamefilledAccountProfile(
            account.Id,
            account.UserName ?? "",
            string.IsNullOrWhiteSpace(account.DisplayName)
                ? account.UserName ?? "" : account.DisplayName,
            account.Bio,
            account.AvatarUrl,
            account.BannerUrl,
            account.CreatedAtUtc,
            account.LastSeenAtUtc,
            account.IsProfilePrivate);
    }

    public async Task<bool> UpdateMyProfileAsync(
        ClaimsPrincipal viewer, string? displayName, string? bio,
        string? avatarUrl, string? bannerUrl, bool isPrivate)
    {
        var id = GamefilledViewer.RequireUserId(viewer);
        var account = await users.FindByIdAsync(id.ToString("D"));
        if (account is null) return false;

        var name = string.IsNullOrWhiteSpace(displayName)
            ? account.UserName : displayName.Trim();
        var about = string.IsNullOrWhiteSpace(bio) ? null : bio.Trim();
        var avatar = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
        var banner = string.IsNullOrWhiteSpace(bannerUrl) ? null : bannerUrl.Trim();

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100
            || about?.Length > 500 || WordCount(about) > 200
            || !ValidImageUrl(avatar) || !ValidImageUrl(banner))
            return false;

        account.DisplayName = name;
        account.Bio = about;
        account.AvatarUrl = avatar;
        account.BannerUrl = banner;
        account.IsProfilePrivate = isPrivate;
        return (await users.UpdateAsync(account)).Succeeded;
    }

    private static int WordCount(string? text) =>
        string.IsNullOrWhiteSpace(text) ? 0 :
        text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    public static bool ValidImageUrl(string? value)
    {
        if (value is null) return true;
        if (value.Length > 500 || value.Any(char.IsControl)) return false;

        // Only same-site paths or absolute HTTPS image links may be rendered.
        if (value.StartsWith('/') && !value.StartsWith("//")
            && !value.StartsWith("/\\") && !value.Contains('\\'))
            return true;

        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && string.IsNullOrEmpty(uri.UserInfo);
    }
}
