using Gamefilled.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gamefilled.Pages.Users
{
    /// <summary>
    /// User login page.
    /// </summary>
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(AppDbContext context, ILogger<LoginModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public string EmailOrUsername { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string? Error { get; set; }

        private const int MaxAttempts = 5;
        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

        private const string KeyFailCount = "login_fail_count";
        private const string KeyLockUntil = "login_lock_until_utc";

        public void OnGet()
        {
            if (!IsSafeLocalReturnUrl(ReturnUrl))
                ReturnUrl = null;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!IsSafeLocalReturnUrl(ReturnUrl))
                ReturnUrl = null;

            var lockUntilUtc = GetLockUntilUtc();
            if (lockUntilUtc != null)
            {
                var nowUtc = DateTimeOffset.UtcNow;

                if (nowUtc < lockUntilUtc.Value)
                {
                    var remaining = lockUntilUtc.Value - nowUtc;
                    var mins = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));

                    Error = $"Too many attempts. Try again in {mins} minute(s).";
                    return Page();
                }

                ClearFailCounterAndLock();
            }

            if (string.IsNullOrWhiteSpace(EmailOrUsername) || string.IsNullOrEmpty(Password))
            {
                Error = "Complete all fields.";
                return Page();
            }

            var identifier = EmailOrUsername.Trim();
            var password = Password;

            if (identifier.Contains("@"))
                identifier = identifier.ToLowerInvariant();

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
                    RegisterFailAttempt();
                    Error = "Invalid username, email or password.";
                    return Page();
                }

                var ok = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                if (!ok)
                {
                    RegisterFailAttempt();
                    Error = "Invalid username, email or password.";
                    return Page();
                }

                ClearFailCounterAndLock();

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

        private void RegisterFailAttempt()
        {
            var count = HttpContext.Session.GetInt32(KeyFailCount) ?? 0;
            count++;
            HttpContext.Session.SetInt32(KeyFailCount, count);

            if (count >= MaxAttempts)
            {
                var lockUntil = DateTimeOffset.UtcNow.Add(LockDuration);
                HttpContext.Session.SetString(KeyLockUntil, lockUntil.ToString("O"));

                _logger.LogWarning(
                    "Login locked for {Minutes} minutes (attempts={Count}).",
                    LockDuration.TotalMinutes,
                    count);
            }
        }

        private DateTimeOffset? GetLockUntilUtc()
        {
            var value = HttpContext.Session.GetString(KeyLockUntil);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return DateTimeOffset.TryParse(value, out var lockUntil)
                ? lockUntil
                : null;
        }

        private void ClearFailCounterAndLock()
        {
            HttpContext.Session.Remove(KeyFailCount);
            HttpContext.Session.Remove(KeyLockUntil);
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
