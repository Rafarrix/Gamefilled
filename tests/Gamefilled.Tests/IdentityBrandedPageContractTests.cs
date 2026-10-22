using Gamefilled.Application.Identity;
using Gamefilled.Application.Identity.PageModels;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Gamefilled.Tests;

/// <summary>
/// Prevent a separate generic V2 UI or a partial login cutover from
/// replacing the real Gamefilled auth pages.
/// </summary>
public sealed class IdentityBrandedPageContractTests
{
    [Fact]
    public void Identity_staged_login_preserves_every_existing_Razor_form_field()
    {
        var currentPage = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "Pages/users/Login.cshtml"));

        foreach (var property in new[]
            { "EmailOrUsername", "Password", "ReturnUrl" })
        {
            Assert.Contains($"asp-for=\"{property}\"", currentPage);
            Assert.NotNull(typeof(GamefilledIdentityLoginPageModel).GetProperty(property));
        }

        Assert.Contains("/lib/imgs/indexbackground.png", currentPage);
        Assert.Contains("/lib/imgs/Gamefilled_Logo_0removebg.png", currentPage);
        Assert.Contains("gf-auth-card", currentPage);
        Assert.Contains("gf-auth-submit", currentPage);
        Assert.Contains("@model Gamefilled.Pages.Users.LoginModel", currentPage);
    }

    [Fact]
    public void Identity_staged_registration_preserves_every_existing_Razor_form_field()
    {
        var currentPage = File.ReadAllText(
            Path.Combine(RepositoryRoot(), "Pages/users/Register.cshtml"));

        foreach (var property in new[]
            { "Username", "Email", "Password", "ConfirmPassword", "AcceptTerms" })
        {
            Assert.Contains($"asp-for=\"{property}\"", currentPage);
            Assert.NotNull(typeof(GamefilledIdentityRegisterPageModel).GetProperty(property));
        }

        Assert.Contains("gf-auth-card", currentPage);
        Assert.Contains("/lib/imgs/indexbackground.png", currentPage);
        Assert.Contains("/lib/imgs/Gamefilled_Logo_0removebg.png", currentPage);
        Assert.Contains("@model RegisterModel", currentPage);
    }

    [Theory]
    [InlineData("Stronger-Passphrase-2026-A!", true)]
    [InlineData("longlonglonglong", false)]
    [InlineData("alllowercase12", false)]
    [InlineData("NOLOWERCASE123!", false)]
    [InlineData("NoNumbersHere!!", false)]
    [InlineData("NoSymbolsHere12", false)]
    [InlineData("Weak12!", false)]
    public void Identity_form_password_validation_matches_Configured_security_policy(
        string password, bool allowed)
    {
        Assert.Equal(allowed, GamefilledIdentityFormPolicy.IsAllowedPassword(password));
    }

    [Theory]
    [InlineData("/", true)]
    [InlineData("/games/100?tab=reviews", true)]
    [InlineData("/Settings/Account", true)]
    [InlineData("https://evil.example", false)]
    [InlineData("//evil.example", false)]
    [InlineData("/\\evil.example", false)]
    [InlineData("/games\\evil.example", false)]
    [InlineData("", false)]
    public void Identity_login_return_locations_stay_inside_gamefilled(
        string candidate, bool allowed)
    {
        Assert.Equal(allowed, GamefilledIdentityFormPolicy.IsSafeReturnUrl(candidate));
    }

    [Fact]
    public void Identity_account_PageModels_are_not_routed_until_full_cutover()
    {
        var root = RepositoryRoot();
        var app = File.ReadAllText(Path.Combine(root, "Program.cs"));

        Assert.DoesNotContain("AddUnifiedGamefilledIdentity(", app);
        Assert.DoesNotContain("app.UseAuthentication(", app);

        // The real UI stays under Pages/users/**; staged replacement PageModels
        // are explicitly outside that routable folder.
        foreach (var type in new[]
        {
            typeof(GamefilledIdentityLoginPageModel),
            typeof(GamefilledIdentityRegisterPageModel),
            typeof(GamefilledIdentityConfirmEmailPageModel),
            typeof(GamefilledIdentityForgotPasswordPageModel),
            typeof(GamefilledIdentityResendConfirmationPageModel),
            typeof(GamefilledIdentityResetPasswordPageModel)
        })
        {
            Assert.Contains(".Application.Identity.PageModels", type.Namespace);
            Assert.True(typeof(Microsoft.AspNetCore.Mvc.RazorPages.PageModel)
                .IsAssignableFrom(type));
        }
    }

    private static string RepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 12 && current is not null; i++, current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Gamefilled.csproj")))
                return current.FullName;
        }
        throw new InvalidOperationException("Could not locate Gamefilled.csproj.");
    }
}
