using Gamefilled.Application.Security;
using Gamefilled.Data;
using Gamefilled.Pages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Xunit;

namespace Gamefilled.Tests;

public sealed class RegistrationInputPolicyTests
{
    private const string StrongPassword = "correct-horse-battery-staple";

    [Fact]
    public void Accepts_valid_input_and_normalizes_only_identifiers()
    {
        var valid = RegistrationInputPolicy.TryValidate(
            "  Test_User  ", "  TEST@Example.com  ", StrongPassword, StrongPassword,
            out var username, out var email, out var error);

        Assert.True(valid, error);
        Assert.Equal("Test_User", username);
        Assert.Equal("test@example.com", email);
        Assert.Equal(string.Empty, error);
    }

    [Theory]
    [InlineData("ab", "user@example.com", StrongPassword, StrongPassword)]
    [InlineData("user with spaces", "user@example.com", StrongPassword, StrongPassword)]
    [InlineData("validuser", "not-an-email", StrongPassword, StrongPassword)]
    [InlineData("validuser", "user@example.com", "123456", "123456")]
    [InlineData("validuser", "user@example.com", StrongPassword, "wrong confirmation")]
    public void Rejects_invalid_registration_input(
        string username, string email, string password, string confirm)
    {
        var valid = RegistrationInputPolicy.TryValidate(
            username, email, password, confirm, out _, out _, out var error);

        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Does_not_trim_a_password_before_comparison()
    {
        var valid = RegistrationInputPolicy.TryValidate(
            "validuser", "user@example.com", "  "+StrongPassword, StrongPassword,
            out _, out _, out var error);

        Assert.False(valid);
        Assert.Equal("Passwords do not match.", error);
    }

    [Fact]
    public async Task Register_post_rejects_missing_terms_without_querying_database()
    {
        var model = new RegisterModel(
            new AppDbContext(new DbContextOptions<AppDbContext>()))
        {
            Username = "validuser",
            Email = "user@example.com",
            Password = StrongPassword,
            ConfirmPassword = StrongPassword,
            AcceptTerms = false
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.NotNull(model.Error);
        Assert.Contains("Terms", model.Error);
    }
}
