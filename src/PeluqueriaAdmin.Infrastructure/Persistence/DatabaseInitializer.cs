using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    IDbContextFactory<PeluqueriaDbContext> contextFactory,
    ApplicationPaths applicationPaths,
    TimeProvider timeProvider)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        applicationPaths.EnsureDirectories();

        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);

        int existingSettings = await context.Settings.CountAsync(cancellationToken);
        if (existingSettings == 0)
        {
            context.Settings.Add(GeneralSettings.CreateDefault(timeProvider.GetUtcNow().UtcDateTime));
            await context.SaveChangesAsync(cancellationToken);
            existingSettings = 1;
        }

        if (existingSettings != 1)
        {
            throw new InvalidOperationException("La base de datos contiene una cantidad inválida de configuraciones generales.");
        }
    }
}
