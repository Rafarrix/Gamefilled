using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages.Users
{
    // ✅ Logout via POST é mais seguro (evita logout por link externo malicioso)
    public class LogoutModel : PageModel
    {
        public IActionResult OnPost()
        {
            // Remove tudo da sessão
            HttpContext.Session.Clear();

            // Redireciona para home
            return RedirectToPage("/Index");
        }

        // Se alguém abrir /Users/Logout no browser, redireciona para home
        public IActionResult OnGet()
        {
            return RedirectToPage("/Index");
        }
    }
}
