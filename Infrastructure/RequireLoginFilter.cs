using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// ✅ Filtro que bloqueia acesso a páginas se não existir sessão "userId".
    /// É usado via Razor Pages conventions (AddFolderApplicationModelConvention).
    /// </summary>
    public class RequireLoginFilter : IPageFilter
    {
        public void OnPageHandlerSelected(PageHandlerSelectedContext context)
        {
            // Não precisamos fazer nada aqui
        }

        public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            // Não precisamos fazer nada aqui
        }

        public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            // ✅ Permite se já houver sessão
            var userId = context.HttpContext.Session.GetInt32("userId");
            if (userId.HasValue)
                return;

            // ✅ Se não houver login, redireciona para Login com returnUrl
            var path = context.HttpContext.Request.Path;
            var query = context.HttpContext.Request.QueryString;
            var returnUrl = (path + query).ToString();

            context.Result = new RedirectToPageResult("/Users/Login", new { returnUrl });
        }
    }
}