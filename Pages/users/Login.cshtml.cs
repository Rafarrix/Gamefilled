using Gamefilled.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages
{
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _context;

        // 🔹 CONSTRUTOR (injeção do DbContext)
        public LoginModel(AppDbContext context)
        {
            _context = context;
        }

        // ===============================
        // CAMPOS DO FORMULÁRIO
        // ===============================

        [BindProperty]
        public string Login { get; set; } = string.Empty;
        // ↑ corresponde a asp-for="Login"

        [BindProperty]
        public string Password { get; set; } = string.Empty;
        // ↑ corresponde a asp-for="Password"

        // ===============================
        // MENSAGEM DE ERRO
        // ===============================
        public string? Error { get; set; }

        // ===============================
        // GET (abrir página)
        // ===============================
        public void OnGet()
        {
            // Não faz nada por agora
        }

        // ===============================
        // POST (submeter login)
        // ===============================
        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
            {
                Error = "Please fill in all fields.";
                return Page();
            }

            // 🔍 VAI À BASE DE DADOS
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Login == Login && u.Password == Password);

            if (user == null)
            {
                Error = "Login inválido.";
                return Page();
            }

            // ✅ LOGIN OK
            return RedirectToPage("/Index");
        }
    }
}
