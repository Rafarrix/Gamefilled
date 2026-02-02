using Gamefilled.Models;
using Microsoft.EntityFrameworkCore;

namespace Gamefilled.Data
{
    public class AppDbContext : DbContext
    {
        // Construtor obrigatório do EF Core
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Representa a tabela Users no SQL
        public DbSet<User> Users { get; set; }
    }
}
