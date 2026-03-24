using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace Gamefilled.Pages.Users
{
    /// <summary>
    /// Página de logout.
    ///
    /// O logout real é feito por POST por motivos de segurança.
    /// </summary>
    public class LogoutModel : PageModel
    {
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(ILogger<LogoutModel> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Logout seguro via POST.
        /// </summary>
        public IActionResult OnPost()
        {
            _logger.LogInformation("Logout POST: clearing session.");

            // Limpa toda a sessão.
            HttpContext.Session.Clear();

            return RedirectToPage("/Index");
        }

        /// <summary>
        /// Se alguém aceder por GET, apenas redireciona para a home.
        /// </summary>
        public IActionResult OnGet()
        {
            return RedirectToPage("/Index");
        }
    }
}