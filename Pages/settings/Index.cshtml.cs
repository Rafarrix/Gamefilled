using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Pages.Settings
{
    public class IndexModel : PageModel
    {
        public string? Error { get; set; }
        public string? Success { get; set; }

        // Campos do “Profile”
        [BindProperty] public string Username { get; set; } = "";
        [BindProperty] public string Bio { get; set; } = "";
        [BindProperty] public string Social1 { get; set; } = "";
        [BindProperty] public string Social2 { get; set; } = "";
        [BindProperty] public string Social3 { get; set; } = "";

        // Features (placeholders)
        [BindProperty] public bool HomeWidgets { get; set; } = true;
        [BindProperty] public bool HighContrast { get; set; } = false;

        public IActionResult OnGet()
        {
            // Guard simples: precisa de sessão
            var userId = HttpContext.Session.GetInt32("userId");
            if (userId == null)
                return RedirectToPage("/Users/Login");

            // Preencher valores iniciais (por agora só usa sessão)
            Username = HttpContext.Session.GetString("username") ?? "user";
            return Page();
        }

        public IActionResult OnPost()
        {
            var userId = HttpContext.Session.GetInt32("userId");
            if (userId == null)
                return RedirectToPage("/Users/Login");

            // Aqui mais tarde: guardar na BD (UserSettings)
            // Por agora só devolvemos sucesso
            Success = "Settings guardados (placeholder).";
            return Page();
        }
    }
}
