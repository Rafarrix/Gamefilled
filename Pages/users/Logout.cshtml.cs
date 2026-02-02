using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages.Users
{
    public class LogoutModel : PageModel
    {
        // GET /users/sign_out
        public IActionResult OnGet()
        {
            // Remove a key "user" da sessão e limpa a sessão para garantir logout completo.
            HttpContext.Session.Remove("user");
            HttpContext.Session.Clear();

            // Redireciona para a página inicial (ou para a página de login)
            return RedirectToPage("/Index");
        }
    }
}
