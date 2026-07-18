using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PeluqueriaAdmin.Infrastructure.Persistence;

public sealed class DesignTimePeluqueriaDbContextFactory
    : IDesignTimeDbContextFactory<PeluqueriaDbContext>
{
    public PeluqueriaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PeluqueriaDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        return new PeluqueriaDbContext(options);
    }
}
