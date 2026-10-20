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

    [Theory]
    [InlineData("12345678901", false)]
    [InlineData("123456789012", true)]
    [InlineData(" 123456789012 ", true)]
    [InlineData("", false)]
    public void Password_policy_is_shared_by_registration_and_account_settings(
        string candidate, bool expected)
    {
        Assert.Equal(expected, RegistrationInputPolicy.IsAllowedPassword(candidate));

        var registrationValid = RegistrationInputPolicy.TryValidate(
            "validuser", "user@example.com", candidate, candidate,
            out _, out _, out _);
        Assert.Equal(expected, registrationValid);
    }

    [Fact]
    public void Password_policy_rejects_more_than_128_characters()
    {
        var candidate = new string('x', 129);
        Assert.False(RegistrationInputPolicy.IsAllowedPassword(candidate));
    }
}
