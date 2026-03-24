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
    /// Comportamento:
    /// - se ainda não existir follow, cria a relação
    /// - se já existir, remove a relação (toggle follow/unfollow)
    /// - regista atividade quando há novo follow
    /// </summary>
    public class FollowModel : PageModel
    {
        /// <summary>
        /// Contexto da base de dados.
        /// </summary>
        private readonly AppDbContext _db;

        public FollowModel(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Handler POST para seguir/deixar de seguir.
        /// </summary>
        /// <param name="username">Username do utilizador alvo.</param>
        /// <param name="returnUrl">URL local para regressar após a ação.</param>
        public async Task<IActionResult> OnPostAsync(string username, string? returnUrl = null)
        {
            // Lê o username do utilizador autenticado a partir da sessão.
            var currentUsername = HttpContext.Session.GetString("username");

            // Se não houver sessão iniciada, redireciona para login.
            if (string.IsNullOrWhiteSpace(currentUsername))
                return RedirectToPage("/users/Login");

            // Se não vier username alvo, devolve 404.
            if (string.IsNullOrWhiteSpace(username))
                return NotFound();

            // Carrega utilizador atual e utilizador alvo.
            var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == currentUsername);
            var targetUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (currentUser == null || targetUser == null)
                return NotFound();

            // Impede seguir a si próprio.
            if (currentUser.Id == targetUser.Id)
                return Redirect(returnUrl ?? $"/u/{username}");

            // Verifica se já existe relação de follow.
            var existingFollow = await _db.Follows.FirstOrDefaultAsync(f =>
                f.FollowerId == currentUser.Id &&
                f.FollowingId == targetUser.Id);

            if (existingFollow == null)
            {
                // Cria novo follow.
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
                // Se já seguia, remove o follow (toggle unfollow).
                _db.Follows.Remove(existingFollow);
            }

            await _db.SaveChangesAsync();

            // Segurança: só aceita returnUrl local.
            if (string.IsNullOrWhiteSpace(returnUrl) || !Url.IsLocalUrl(returnUrl))
                return Redirect($"/u/{username}");

            return Redirect(returnUrl);
        }
    }
}