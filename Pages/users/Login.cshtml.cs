using Gamefilled.Application.Security;
using Gamefilled.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gamefilled.Pages.Users
{
    /// <summary>
    /// User login page.
    /// </summary>
    [EnableRateLimiting("login-ip")]
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LoginModel> _logger;
        private readonly LoginAttemptGuard _attempts;

        public LoginModel(AppDbContext context, ILogger<LoginModel> logger, LoginAttemptGuard attempts)
        {
            _context = context;
            _logger = logger;
            _attempts = attempts;
        }

        [BindProperty]
        public string EmailOrUsername { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string? Error { get; set; }

        public void OnGet()
        {
            if (!IsSafeLocalReturnUrl(ReturnUrl))
                ReturnUrl = null;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!IsSafeLocalReturnUrl(ReturnUrl))
                ReturnUrl = null;

            if (string.IsNullOrWhiteSpace(EmailOrUsername) || string.IsNullOrEmpty(Password))
            {
                Error = "Complete all fields.";
                return Page();
            }

            var identifier = EmailOrUsername.Trim();
            var password = Password;

            if (identifier.Contains("@"))
                identifier = identifier.ToLowerInvariant();

            // Never allocate a rate-limit cache key from unbounded user input.
            if (identifier.Length > 150)
            {
                Error = "Invalid username, email or password.";
                return Page();
            }

            if (_attempts.IsBlocked(identifier, out var remainingMinutes))
            {
                Error = $"Too many attempts. Try again in {remainingMinutes} minute(s).";
                return Page();
            }

            try
            {
                _logger.LogInformation(
                    "Login attempt. DB={DbName}",
                    _context.Database.GetDbConnection().Database);

                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    (u.Username != null && u.Username == identifier) ||
                    (u.Email != null && u.Email == identifier));

                if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
                {
                    _attempts.RecordFailure(identifier);
                    Error = "Invalid username, email or password.";
                    return Page();
                }

                var ok = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                if (!ok)
                {
                    _attempts.RecordFailure(identifier);
                    Error = "Invalid username, email or password.";
                    return Page();
                }

                _attempts.RecordSuccess(identifier);

                var now = DateTime.UtcNow;
                user.LastSeenAt = now;
                await _context.SaveChangesAsync();

                var usernameForSession = !string.IsNullOrWhiteSpace(user.Username)
                    ? user.Username.Trim()
                    : $"user{user.Id}";

                var displayNameForSession = !string.IsNullOrWhiteSpace(user.DisplayName)
                    ? user.DisplayName.Trim()
                    : usernameForSession;

                HttpContext.Session.SetInt32("userId", user.Id);
                HttpContext.Session.SetString("username", usernameForSession);
                HttpContext.Session.SetString("displayName", displayNameForSession);
                HttpContext.Session.SetString("role", user.Role ?? "User");
                HttpContext.Session.SetString("presence_last_touch_utc", now.ToString("O"));

                if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
                    HttpContext.Session.SetString("avatarUrl", user.AvatarUrl.Trim());
                else
                    HttpContext.Session.Remove("avatarUrl");

                if (!string.IsNullOrWhiteSpace(ReturnUrl))
                    return LocalRedirect(ReturnUrl);

                return RedirectToPage("/Index");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Login error for {Identifier}", identifier);
                Error = "An unexpected error occurred.";
                return Page();
            }
        }

        private bool IsSafeLocalReturnUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Url.IsLocalUrl(url)) return false;
            if (url.StartsWith("//") || url.StartsWith("/\\")) return false;

            return true;
        }
    }
}
