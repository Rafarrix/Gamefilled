using Gamefilled.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages
{
    public class RegisterModel : PageModel
    {
        // ===============================
        // CAMPOS DO FORMULÁRIO
        // ===============================

        [BindProperty]
        public string Username { get; set; } = string.Empty;
        // ↑ corresponde a asp-for="Username"

        [BindProperty]
        public string Email { get; set; } = string.Empty;
        // ↑ corresponde a asp-for="Email"

        [BindProperty]
        public string Password { get; set; } = string.Empty;
        // ↑ corresponde a asp-for="Password"

        [BindProperty]
        public string ConfirmPassword { get; set; } = string.Empty;
        // ↑ corresponde a asp-for="ConfirmPassword"

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
        // POST (submeter registo)
        // ===============================
        public IActionResult OnPost()
        {
            // Valida campos preenchidos
            if (string.IsNullOrWhiteSpace(Username) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                Error = "Please fill in all fields.";
                return Page();
            }

            // Valida correspondência de passwords
            if (Password != ConfirmPassword)
            {
                Error = "Passwords do not match.";
                return Page();
            }

            // Validação simples de comprimento da password (ajuste conforme necessário)
            if (Password.Length < 6)
            {
                Error = "Password must be at least 6 characters.";
                return Page();
            }

            // 🔒 Ponto de extensão: aqui deve ir a lógica para verificar se o user/email já existe
            // e para persistir o novo utilizador na base de dados (ex.: via AppDbContext).
            // Por ora devolve ao login após registo simulado.

            return RedirectToPage("/users/Login");
        }
    }
}
