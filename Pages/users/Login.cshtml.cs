using Gamefilled.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gamefilled.Pages.Users
{
    /// <summary>
    /// Página de login do utilizador.
    ///
    /// Responsabilidades:
    /// - validar credenciais
    /// - aplicar rate limit por sessão
    /// - criar sessão autenticada
    /// - redirecionar para ReturnUrl local segura
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

        // Campo único: email ou username.
        [BindProperty]
        public string EmailOrUsername { get; set; } = string.Empty;

        // Password introduzida no form.
        [BindProperty]
        public string Password { get; set; } = string.Empty;

        // ReturnUrl segura para pós-login.
        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        // Mensagem de erro para mostrar na view.
        public string? Error { get; set; }

        /* =====================================================================
           RATE LIMIT
           ===================================================================== */

        private const int MaxAttempts = 5;
        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

        private const string KeyFailCount = "login_fail_count";
        private const string KeyLockUntil = "login_lock_until_utc";

        /* =====================================================================
           GET
           ===================================================================== */

        public void OnGet()
        {
            // Só aceita ReturnUrl local e segura.
            if (!IsSafeLocalReturnUrl(ReturnUrl))
            {
                ReturnUrl = null;
            }
        }

        /* =====================================================================
           POST
           ===================================================================== */

        public async Task<IActionResult> OnPostAsync()
        {
            // Revalida ReturnUrl.
            if (!IsSafeLocalReturnUrl(ReturnUrl))
            {
                ReturnUrl = null;
            }

            // Se está bloqueado por demasiadas tentativas falhadas.
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
                    // Se o bloqueio já expirou, limpa os dados.
                    ClearFailCounterAndLock();
                }
            }

            // Validação básica dos campos.
            if (string.IsNullOrWhiteSpace(EmailOrUsername) || string.IsNullOrWhiteSpace(Password))
            {
                Error = "Preenche todos os campos.";
                return Page();
            }

            // Normalização.
            var identifier = EmailOrUsername.Trim();
            var password = Password.Trim();

            if (identifier.Contains("@"))
                identifier = identifier.ToLowerInvariant();

            try
            {
                _logger.LogInformation("Login attempt. DB={DbName}", _context.Database.GetDbConnection().Database);

                // Procura user por username ou email.
                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    (u.Username != null && u.Username == identifier) ||
                    (u.Email != null && u.Email == identifier));

                if (user == null)
                {
                    RegisterFailAttempt();
                    Error = "Utilizador não encontrado.";
                    return Page();
                }

                // Garante que a conta tem password hash.
                if (string.IsNullOrWhiteSpace(user.PasswordHash))
                {
                    RegisterFailAttempt();
                    Error = "Conta sem password definida.";
                    return Page();
                }

                // Verifica password com BCrypt.
                var ok = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                if (!ok)
                {
                    RegisterFailAttempt();
                    Error = "Password incorreta.";
                    return Page();
                }

                // Login com sucesso -> limpa rate limit.
                ClearFailCounterAndLock();

                // Valores para sessão.
                var usernameForSession = !string.IsNullOrWhiteSpace(user.Username)
                    ? user.Username
                    : $"user{user.Id}";

                var displayNameForSession = !string.IsNullOrWhiteSpace(user.Username)
                    ? user.Username
                    : (!string.IsNullOrWhiteSpace(user.Email) ? user.Email : $"user{user.Id}");

                // Cria sessão autenticada.
                HttpContext.Session.SetInt32("userId", user.Id);
                HttpContext.Session.SetString("username", usernameForSession);
                HttpContext.Session.SetString("displayName", displayNameForSession);
                HttpContext.Session.SetString("role", user.Role ?? "User");

                // Redireciona para ReturnUrl se existir.
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

        /* =====================================================================
           HELPERS DE RATE LIMIT
           ===================================================================== */

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

        /* =====================================================================
           HELPER DE SEGURANÇA: RETURNURL
           ===================================================================== */

        /// <summary>
        /// Impede open redirect, aceitando apenas URLs locais.
        /// </summary>
        private bool IsSafeLocalReturnUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Url.IsLocalUrl(url)) return false;

            if (url.StartsWith("//") || url.StartsWith("/\\"))
                return false;

            return true;
        }
    }
}