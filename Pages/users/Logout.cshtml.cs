using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Gamefilled.Pages.Users
{
    // ✅ Logout via POST (seguro)
    public class LogoutModel : PageModel
    {
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(ILogger<LogoutModel> logger)
        {
            _logger = logger;
        }

        public IActionResult OnPost()
        {
            _logger.LogInformation("Logout POST: clearing session.");

            // Limpa a sessão inteira
            HttpContext.Session.Clear();

            // Volta à home
            return RedirectToPage("/Index");
        }

        // Se alguém abrir /users/Logout no browser
        public IActionResult OnGet()
        {
            return RedirectToPage("/Index");
        }
    }
}
