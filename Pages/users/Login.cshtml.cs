using Gamefilled.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gamefilled.Pages
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

        // Campo único: Email OU Username
        [BindProperty]
        public string EmailOrUsername { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string? Error { get; set; }

        // ===============================
        // CONFIG DO RATE LIMIT
        // ===============================
        private const int MaxAttempts = 5;
        private static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(5);

        // Chaves na Session
        private const string KeyFailCount = "login_fail_count";
        private const string KeyLockUntil = "login_lock_until_utc"; // ISO string

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            // -------------------------------
            // 0) Rate limit: está bloqueado?
            // -------------------------------
            var lockUntilUtc = GetLockUntilUtc();
            if (lockUntilUtc != null)
            {
                var nowUtc = DateTimeOffset.UtcNow;

                // Ainda está bloqueado
                if (nowUtc < lockUntilUtc.Value)
                {
                    var remaining = lockUntilUtc.Value - nowUtc;
                    var mins = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));

                    Error = $"Demasiadas tentativas. Tenta novamente daqui a {mins} minuto(s).";
                    return Page();
                }
                else
                {
                    // Já passou o bloqueio -> limpa lock
                    ClearLock();
                }
            }

            // 1) Validar campos
            if (string.IsNullOrWhiteSpace(EmailOrUsername) || string.IsNullOrWhiteSpace(Password))
            {
                Error = "Preenche todos os campos.";
                return Page();
            }

            // 2) Normalizar inputs
            var identifier = EmailOrUsername.Trim();
            var password = Password.Trim();

            // Se for email, normaliza para minúsculas
            if (identifier.Contains("@"))
                identifier = identifier.ToLowerInvariant();

            try
            {
                _logger.LogInformation("Login attempt. DB={DbName}", _context.Database.GetDbConnection().Database);

                // 3) Procurar user por Username OU Email
                var user = await _context.Users.FirstOrDefaultAsync(u =>
                    (u.Username != null && u.Username == identifier) ||
                    (u.Email != null && u.Email == identifier));

                if (user == null)
                {
                    RegisterFailAttempt();
                    Error = "Utilizador não encontrado.";
                    return Page();
                }

                // 4) Verificar hash
                if (string.IsNullOrWhiteSpace(user.PasswordHash))
                {
                    // Conta “inválida” (não tem hash)
                    RegisterFailAttempt();
                    Error = "Conta sem password definida.";
                    return Page();
                }

                // BCrypt verify
                var ok = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                if (!ok)
                {
                    RegisterFailAttempt();
                    Error = "Password incorreta.";
                    return Page();
                }

                // ✅ Sucesso: limpa contador de falhas/lock
                ClearFailCounterAndLock();

                // 5) Sessão (nunca null)
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
        // Helpers de Rate Limit (Session)
        // ===============================

        private void RegisterFailAttempt()
        {
            // Incrementa falhas
            var count = HttpContext.Session.GetInt32(KeyFailCount) ?? 0;
            count++;
            HttpContext.Session.SetInt32(KeyFailCount, count);

            // Se atingiu o limite, bloqueia
            if (count >= MaxAttempts)
            {
                var lockUntil = DateTimeOffset.UtcNow.Add(LockDuration);

                // Guardar em ISO para ser fácil de parse
                HttpContext.Session.SetString(KeyLockUntil, lockUntil.ToString("O"));

                // (Opcional) log
                _logger.LogWarning("Login locked for {Minutes} minutes (attempts={Count}).", LockDuration.TotalMinutes, count);
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

        private void ClearLock()
        {
            HttpContext.Session.Remove(KeyLockUntil);
        }

        private void ClearFailCounterAndLock()
        {
            HttpContext.Session.Remove(KeyFailCount);
            HttpContext.Session.Remove(KeyLockUntil);
        }
    }
}