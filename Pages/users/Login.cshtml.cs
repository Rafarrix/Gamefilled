using Gamefilled.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gamefilled.Pages.Users
{
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _context;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(AppDbContext context, ILogger<LoginModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        // ===============================
        // INPUTS DO FORM
        // ===============================

        // Campo único: Email OU Username
        [BindProperty]
        public string EmailOrUsername { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        // ✅ returnUrl precisa de ser BindProperty também,
        // para sobreviver do OnGet -> form -> OnPost
        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string? Error { get; set; }

        // ===============================
        // RATE LIMIT (5 tentativas / 5 min)
        // ===============================
        private const int MaxAttempts = 5;
        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

        private const string KeyFailCount = "login_fail_count";
        private const string KeyLockUntil = "login_lock_until_utc"; // ISO string

        // ===============================
        // GET
        // ===============================
        public void OnGet()
        {
            // ✅ Segurança: se ReturnUrl vier vazio ou não for local, ignora.
            if (!IsSafeLocalReturnUrl(ReturnUrl))
            {
                ReturnUrl = null;
            }
        }

        // ===============================
        // POST
        // ===============================
        public async Task<IActionResult> OnPostAsync()
        {
            // -------------------------------
            // 0) Segurança do ReturnUrl
            // -------------------------------
            if (!IsSafeLocalReturnUrl(ReturnUrl))
            {
                ReturnUrl = null;
            }

            // -------------------------------
            // 1) Rate limit: está bloqueado?
            // -------------------------------
            var lockUntilUtc = GetLockUntilUtc();
            if (lockUntilUtc != null)
            {
                var nowUtc = DateTimeOffset.UtcNow;

                if (nowUtc < lockUntilUtc.Value)
                {
                    var remaining = lockUntilUtc.Value - nowUtc;
                    var mins = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));

                    Error = $"Demasiadas tentativas. Tenta novamente daqui a {mins} minuto(s).";
                    return Page();
                }
                else
                {
                    // ✅ CORREÇÃO: lock expirou -> limpa lock + contador
                    ClearFailCounterAndLock();
                }
            }

            // -------------------------------
            // 2) Validar campos
            // -------------------------------
            if (string.IsNullOrWhiteSpace(EmailOrUsername) || string.IsNullOrWhiteSpace(Password))
            {
                Error = "Preenche todos os campos.";
                return Page();
            }

            // -------------------------------
            // 3) Normalizar inputs
            // -------------------------------
            var identifier = EmailOrUsername.Trim();
            var password = Password.Trim();

            if (identifier.Contains("@"))
                identifier = identifier.ToLowerInvariant();

            try
            {
                _logger.LogInformation("Login attempt. DB={DbName}", _context.Database.GetDbConnection().Database);

                // -------------------------------
                // 4) Procurar user por Username OU Email
                // -------------------------------
                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    (u.Username != null && u.Username == identifier) ||
                    (u.Email != null && u.Email == identifier));

                if (user == null)
                {
                    RegisterFailAttempt();
                    Error = "Utilizador não encontrado.";
                    return Page();
                }

                // -------------------------------
                // 5) Validar PasswordHash
                // -------------------------------
                if (string.IsNullOrWhiteSpace(user.PasswordHash))
                {
                    RegisterFailAttempt();
                    Error = "Conta sem password definida.";
                    return Page();
                }

                var ok = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                if (!ok)
                {
                    RegisterFailAttempt();
                    Error = "Password incorreta.";
                    return Page();
                }

                // ✅ Sucesso: limpa rate limit
                ClearFailCounterAndLock();

                // -------------------------------
                // 6) Criar sessão
                // -------------------------------
                var usernameForSession = !string.IsNullOrWhiteSpace(user.Username)
                    ? user.Username
                    : $"user{user.Id}";

                var displayNameForSession = !string.IsNullOrWhiteSpace(user.Username)
                    ? user.Username
                    : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email : $"user{user.Id}");

                HttpContext.Session.SetInt32("userId", user.Id);
                HttpContext.Session.SetString("username", usernameForSession);
                HttpContext.Session.SetString("displayName", displayNameForSession);
                HttpContext.Session.SetString("role", user.Role ?? "User");

                // -------------------------------
                // 7) Redirect inteligente (ReturnUrl)
                // -------------------------------
                // ✅ Se veio de uma página protegida (ex: /Settings),
                // volta para lá. Senão vai para /Index.
                if (!string.IsNullOrWhiteSpace(ReturnUrl))
                    return LocalRedirect(ReturnUrl);

                return RedirectToPage("/Index");
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Erro no login para {Identifier}", identifier);
                Error = "Ocorreu um erro inesperado.";
                return Page();
            }
        }

        // ===============================
        // HELPERS: Rate limit (Session)
        // ===============================

        private void RegisterFailAttempt()
        {
            var count = HttpContext.Session.GetInt32(KeyFailCount) ?? 0;
            count++;
            HttpContext.Session.SetInt32(KeyFailCount, count);

            if (count >= MaxAttempts)
            {
                var lockUntil = DateTimeOffset.UtcNow.Add(LockDuration);
                HttpContext.Session.SetString(KeyLockUntil, lockUntil.ToString("O"));

                _logger.LogWarning("Login locked for {Minutes} minutes (attempts={Count}).",
                    LockDuration.TotalMinutes, count);
            }
        }

        private DateTimeOffset? GetLockUntilUtc()
        {
            var s = HttpContext.Session.GetString(KeyLockUntil);
            if (string.IsNullOrWhiteSpace(s)) return null;

            if (DateTimeOffset.TryParse(s, out var dto))
                return dto;

            return null;
        }

        private void ClearFailCounterAndLock()
        {
            HttpContext.Session.Remove(KeyFailCount);
            HttpContext.Session.Remove(KeyLockUntil);
        }

        // ===============================
        // HELPERS: ReturnUrl seguro
        // ===============================

        /// <summary>
        /// ✅ Impede Open Redirect:
        /// só aceitamos URLs locais do teu site (ex: "/Settings", "/u/admin").
        /// </summary>
        private bool IsSafeLocalReturnUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            // Tem de ser local
            if (!Url.IsLocalUrl(url)) return false;

            // Bloqueia //example.com ou /\evil
            if (url.StartsWith("//") || url.StartsWith("/\\"))
                return false;

            return true;
        }
    }
}
