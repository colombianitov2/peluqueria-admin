using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Infrastructure.Persistence;

public sealed class PeluqueriaDbContext(DbContextOptions<PeluqueriaDbContext> options)
    : DbContext(options)
{
    public DbSet<GeneralSettings> Settings => Set<GeneralSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PeluqueriaDbContext).Assembly);
    }
}
