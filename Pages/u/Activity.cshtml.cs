using Gamefilled.Data;
using Gamefilled.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Pages.u
{
    /// <summary>
    /// Página de atividade pública de um utilizador.
    /// </summary>
    public class ActivityModel : PageModel
    {
        /// <summary>
        /// Contexto da base de dados.
        /// </summary>
        private readonly AppDbContext _db;

        public ActivityModel(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Utilizador dono do perfil.
        /// </summary>
        public User ProfileUser { get; set; } = default!;

        /// <summary>
        /// Lista de itens de atividade preparados para a UI.
        /// </summary>
        public List<ActivityItemViewModel> ActivityItems { get; set; } = new();

        /// <summary>
        /// Carrega a página de atividade do utilizador.
        /// </summary>
        public async Task<IActionResult> OnGetAsync(string username, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(username))
                return NotFound();

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
            if (user == null)
                return NotFound();

            ProfileUser = user;

            // Vai buscar até 50 atividades mais recentes.
            var activities = await _db.UserActivities
                .Where(x => x.UserId == user.Id)
                .Include(x => x.TargetUser)
                .OrderByDescending(x => x.CreatedAt)
                .Take(50)
                .ToListAsync(ct);

            // Converte para ViewModel simples.
            ActivityItems = activities
                .Select(x => new ActivityItemViewModel
                {
                    Type = x.Type,
                    CreatedAt = x.CreatedAt,
                    TargetUsername = x.TargetUser?.Username,
                    TargetDisplayName = x.TargetUser?.DisplayName
                })
                .ToList();

            return Page();
        }

        /// <summary>
        /// ViewModel interno para representar um item de atividade.
        /// </summary>
        public class ActivityItemViewModel
        {
            public string Type { get; set; } = "";
            public DateTime CreatedAt { get; set; }

            public string? TargetUsername { get; set; }
            public string? TargetDisplayName { get; set; }
        }
    }
}