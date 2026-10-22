using Gamefilled.Application.Identity;
using Gamefilled.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Identity;

/// <summary>
/// The reviewed authentication configuration to activate atomically at the
/// final cutover. Deliberately NOT called by Program while legacy sessions
/// authorize any page or mutation.
/// </summary>
public static class GamefilledIdentityRegistration
{
    public static IServiceCollection AddUnifiedGamefilledIdentity(
        this IServiceCollection services, string isolatedConnection)
    {
        if (string.IsNullOrWhiteSpace(isolatedConnection))
            throw new ArgumentException("A dedicated V2 SQL connection is required.",
                nameof(isolatedConnection));

        services.AddDbContext<GamefilledV2DbContext>(options =>
            options.UseSqlServer(isolatedConnection,
                sql => sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));

        services.AddIdentityCore<GamefilledAccount>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequiredLength = 12;
                options.Password.RequiredUniqueChars = 3;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Tokens.EmailConfirmationTokenProvider =
                    TokenOptions.DefaultProvider;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddSignInManager()
            .AddEntityFrameworkStores<GamefilledV2DbContext>()
            .AddDefaultTokenProviders();

        services.Configure<SecurityStampValidatorOptions>(options =>
            options.ValidationInterval = TimeSpan.FromMinutes(1));
        services.Configure<DataProtectionTokenProviderOptions>(options =>
            options.TokenLifespan = TimeSpan.FromHours(1));

        services.AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Cookie.Name = ".Gamefilled.V2.Identity";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.LoginPath = "/users/Login";
                options.AccessDeniedPath = "/Error";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(2);
                options.Events.OnRedirectToLogin = context =>
                {
                    // API requests should receive 401 instead of login HTML.
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            });

        services.AddScoped<GamefilledIdentityAccountService>();
        services.AddScoped<GamefilledIdentitySignInService>();
        services.AddScoped<GamefilledIdentityProfileService>();
        services.AddScoped<GamefilledIdentityAccountSettingsService>();
        services.AddSingleton<GamefilledIdentityLinkFactory>();
        return services;
    }
}
