using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.users
{
    /// <summary>
    /// Handler responsável por seguir ou deixar de seguir um utilizador.
    ///
    /// O que este ficheiro faz:
    /// - verifica quem é o utilizador autenticado
    /// - valida o utilizador alvo
    /// - verifica se já existe follow
    /// - cria ou remove a relação de follow
    /// - regista atividade quando existe novo follow
    /// - guarda tudo na base de dados
    ///
    /// Importância:
    /// Este ficheiro é ótimo para mostrar no vídeo porque demonstra:
    /// - uso real da base de dados
    /// - escrita de dados
    /// - lógica de negócio
    /// </summary>
    public class FollowModel : PageModel
    {
        /// <summary>
        /// Contexto da base de dados.
        /// É através dele que a página lê e grava informação.
        /// </summary>
        private readonly AppDbContext _db;

        public FollowModel(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Handler POST que executa a ação de seguir ou deixar de seguir.
        /// </summary>
        /// <param name="username">Username do utilizador alvo.</param>
        /// <param name="returnUrl">URL local para regressar após a ação.</param>
        public async Task<IActionResult> OnPostAsync(string username, string? returnUrl = null)
        {
            // =========================================================
            // 1) IDENTIFICAR O UTILIZADOR AUTENTICADO
            // =========================================================
            // O username do utilizador atual é lido da sessão.
            var currentUsername = HttpContext.Session.GetString("username");

            // Se não houver sessão, obriga a login.
            if (string.IsNullOrWhiteSpace(currentUsername))
                return RedirectToPage("/users/Login");

            // Se não vier o username alvo, devolve 404.
            if (string.IsNullOrWhiteSpace(username))
                return NotFound();

            // =========================================================
            // 2) CARREGAR UTILIZADORES ENVOLVIDOS
            // =========================================================
            var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == currentUsername);
            var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

            // Se algum não existir, devolve 404.
            if (currentUser == null || targetUser == null)
                return NotFound();

            // Impede que um utilizador siga a si próprio.
            if (currentUser.Id == targetUser.Id)
                return Redirect(returnUrl ?? $"/u/{username}");

            // =========================================================
            // 3) VERIFICAR SE JÁ EXISTE FOLLOW
            // =========================================================
            var existingFollow = await _db.Follows.FirstOrDefaultAsync(f =>
                f.FollowerId == currentUser.Id &&
                f.FollowingId == targetUser.Id);

            if (existingFollow == null)
            {
                // =====================================================
                // 4A) CRIAR NOVO FOLLOW
                // =====================================================
                _db.Follows.Add(new Follow
                {
                    FollowerId = currentUser.Id,
                    FollowingId = targetUser.Id,
                    CreatedAt = DateTime.UtcNow
                });

                // Regista atividade do utilizador.
                _db.UserActivities.Add(new UserActivity
                {
                    UserId = currentUser.Id,
                    Type = "followed_user",
                    TargetUserId = targetUser.Id,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                // =====================================================
                // 4B) REMOVER FOLLOW EXISTENTE
                // =====================================================
                // Se já seguia, faz unfollow.
                _db.Follows.Remove(existingFollow);
            }

            // =========================================================
            // 5) GUARDAR ALTERAÇÕES NA BASE DE DADOS
            // =========================================================
            await _db.SaveChangesAsync();

            // =========================================================
            // 6) REDIRECIONAR COM SEGURANÇA
            // =========================================================
            // Só aceita returnUrl local para evitar open redirect.
            if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
                return Redirect($"/u/{username}");

            return Redirect(returnUrl);
        }
    }
}