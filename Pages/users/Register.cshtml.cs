using Gamefilled.Application.Security;
using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.RateLimiting;

namespace Gamefilled.Pages
{
    /// <summary>
    /// Página de registo de novo utilizador.
    ///
    /// Responsabilidades:
    /// - validar dados de registo
    /// - garantir unicidade de username/email
    /// - gerar hash da password com BCrypt
    /// - criar conta na base de dados
    /// </summary>
    [EnableRateLimiting("registration-ip")]
    public class RegisterModel : PageModel
    {
        private readonly AppDbContext _context;

        /// <summary>
        /// Work factor do BCrypt.
        /// Quanto maior, mais segura mas mais lenta a operação.
        /// </summary>
        private const int BcryptWorkFactor = 11;

        public RegisterModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty] public string Username { get; set; } = string.Empty;
        [BindProperty] public string Email { get; set; } = string.Empty;
        [BindProperty] public string Password { get; set; } = string.Empty;
        [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;
        [BindProperty] public bool AcceptTerms { get; set; }

        public string? Error { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            // Validate before database queries or expensive BCrypt hashing.
            if (!RegistrationInputPolicy.TryValidate(
                    Username, Email, Password, ConfirmPassword,
                    out var username, out var email, out var validationError))
            {
                Error = validationError;
                return Page();
            }

            // This checkbox must also be enforced on the server, not only with HTML 'required'.
            if (!AcceptTerms)
            {
                Error = "You must accept the Terms of Service and Privacy Policy.";
                return Page();
            }

            // 6) Username único.
            if (await _context.Users.AnyAsync(u => u.Username != null && u.Username == username))
            {
                Error = "That username is already taken.";
                return Page();
            }

            // 7) Email único.
            if (await _context.Users.AnyAsync(u => u.Email != null && u.Email == email))
            {
                Error = "That email is already in use.";
                return Page();
            }

            // 8) Gera hash seguro da password.
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(Password, workFactor: BcryptWorkFactor);

            // 9) Cria novo utilizador.
            var newUser = new User
            {
                Username = username,
                Email = email,
                PasswordHash = passwordHash,
                Role = "User",
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // Redireciona para login.
            return RedirectToPage("/Users/Login");
        }
    }
}