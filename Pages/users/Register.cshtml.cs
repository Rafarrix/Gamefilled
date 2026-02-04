using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly AppDbContext _context;

        private const int BcryptWorkFactor = 11;

        public RegisterModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty] public string Username { get; set; } = string.Empty;
        [BindProperty] public string Email { get; set; } = string.Empty;
        [BindProperty] public string Password { get; set; } = string.Empty;
        [BindProperty] public string ConfirmPassword { get; set; } = string.Empty;

        public string? Error { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            // 1) Campos preenchidos
            if (string.IsNullOrWhiteSpace(Username) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                Error = "Please fill in all fields.";
                return Page();
            }

            // 2) Normalizar
            var username = Username.Trim();
            var email = Email.Trim().ToLowerInvariant();
            var password = Password.Trim();
            var confirm = ConfirmPassword.Trim();

            // 3) Passwords iguais
            if (password != confirm)
            {
                Error = "Passwords do not match.";
                return Page();
            }

            // 4) Password mínima
            if (password.Length < 6)
            {
                Error = "Password must be at least 6 characters.";
                return Page();
            }

            // 5) Email básico
            if (!email.Contains("@"))
            {
                Error = "Please enter a valid email.";
                return Page();
            }

            // 6) Username único
            if (await _context.Users.AnyAsync(u => u.Username != null && u.Username == username))
            {
                Error = "That username is already taken.";
                return Page();
            }

            // 7) Email único
            if (await _context.Users.AnyAsync(u => u.Email != null && u.Email == email))
            {
                Error = "That email is already in use.";
                return Page();
            }

            // 8) Hash da password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: BcryptWorkFactor);

            // 9) Criar utilizador
            var newUser = new User
            {
                Username = username,
                Email = email,
                PasswordHash = passwordHash, // ✅ único campo de password que guardamos
                Role = "User",
                CreatedAt = DateTime.Now
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return RedirectToPage("/Users/Login");
        }
    }
}
