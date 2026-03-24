using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// Classe base para páginas Razor que exigem utilizador autenticado por sessão.
    ///
    /// Objetivo:
    /// - Centralizar lógica comum de verificação de login
    /// - Evitar repetir código em vários PageModels
    /// </summary>
    public abstract class ProtectedPageModel : PageModel
    {
        /// <summary>
        /// ID do utilizador autenticado na sessão atual.
        /// Devolve null se não houver sessão iniciada.
        /// </summary>
        public int? CurrentUserId => HttpContext.Session.GetInt32("userId");

        /// <summary>
        /// Indica se existe utilizador autenticado.
        /// </summary>
        public bool IsLoggedIn => CurrentUserId.HasValue;

        /// <summary>
        /// Deve ser chamado no início de handlers OnGet/OnPost de páginas protegidas.
        ///
        /// Se não houver login:
        /// - redireciona para /Users/Login
        /// - inclui returnUrl para voltar à página desejada
        ///
        /// Se houver login:
        /// - devolve null, permitindo continuar a execução normal
        /// </summary>
        protected IActionResult? RequireLogin()
        {
            if (!IsLoggedIn)
            {
                // Guarda o caminho atual para redirecionar depois do login.
                var returnUrl = $"{Request.Path}{Request.QueryString}";
                return RedirectToPage("/Users/Login", new { returnUrl });
            }

            return null;
        }
    }
}