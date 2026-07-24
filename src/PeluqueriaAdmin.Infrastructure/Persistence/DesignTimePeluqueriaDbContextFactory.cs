using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PeluqueriaAdmin.Infrastructure.Persistence;

public sealed class DesignTimePeluqueriaDbContextFactory
    : IDesignTimeDbContextFactory<PeluqueriaDbContext>
{
    public PeluqueriaDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<PeluqueriaDbContext>();
        DatabaseConfiguration.Configure(builder, ":memory:");
        DbContextOptions<PeluqueriaDbContext> options = builder.Options;

        return new PeluqueriaDbContext(options);
    }
}
