using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Gamefilled.Infrastructure
{
    /// <summary>
    /// Filtro para Razor Pages que bloqueia o acesso
    /// se não existir sessão com "userId".
    ///
    /// Uso típico:
    /// - aplicado por convenções no Program.cs
    /// - útil para proteger pastas inteiras sem repetir código
    /// </summary>
    public class RequireLoginFilter : IPageFilter
    {
        /// <summary>
        /// Executa depois de o handler ter sido selecionado.
        /// Não é usado neste filtro.
        /// </summary>
        public void OnPageHandlerSelected(PageHandlerSelectedContext context)
        {
            // Não precisamos fazer nada aqui.
        }

        /// <summary>
        /// Executa depois do handler correr.
        /// Não é usado neste filtro.
        /// </summary>
        public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            // Não precisamos fazer nada aqui.
        }

        /// <summary>
        /// Executa antes do handler da página.
        /// Aqui é feita a verificação de sessão/login.
        /// </summary>
        public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            // Verifica se existe userId na sessão.
            var userId = context.HttpContext.Session.GetInt32("userId");

            // Se existir login, deixa a página continuar normalmente.
            if (userId.HasValue)
                return;

            // Se não existir login, constrói a returnUrl
            // para o utilizador voltar à página após autenticação.
            var path = context.HttpContext.Request.Path;
            var query = context.HttpContext.Request.QueryString;
            var returnUrl = (path + query).ToString();

            // Interrompe a execução normal e redireciona para a página de login.
            context.Result = new RedirectToPageResult("/Users/Login", new { returnUrl });
        }
    }
}