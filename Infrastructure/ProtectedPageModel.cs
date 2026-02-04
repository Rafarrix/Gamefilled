using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Infrastructure
{
    // ✅ Base class para páginas que exigem login (sessão)
    public abstract class ProtectedPageModel : PageModel
    {
        public int? CurrentUserId => HttpContext.Session.GetInt32("userId");
        public bool IsLoggedIn => CurrentUserId.HasValue;

        // Chama isto no início do OnGet/OnPost das páginas protegidas
        protected IActionResult? RequireLogin()
        {
            if (!IsLoggedIn)
            {
                // Guarda para onde o user queria ir (opcional)
                var returnUrl = $"{Request.Path}{Request.QueryString}";
                return RedirectToPage("/Users/Login", new { returnUrl });
            }

            return null;
        }
    }
}