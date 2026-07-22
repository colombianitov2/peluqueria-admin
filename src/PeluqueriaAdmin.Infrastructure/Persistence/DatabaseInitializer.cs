using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Persistence;

public sealed class DatabaseInitializer(
    IDbContextFactory<PeluqueriaDbContext> contextFactory,
    ApplicationPaths applicationPaths,
    TimeProvider timeProvider,
    DatabaseBackupService? backupService = null)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        applicationPaths.EnsureDirectories();

        if (backupService is not null
            && await backupService.HasPendingSchemaChangesAsync(cancellationToken))
        {
            await backupService.CreateBeforeMigrationAsync(cancellationToken);
        }

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

        GeneralSettings settings = await context.Settings.SingleAsync(cancellationToken);
        if (settings.NormalizeRetiredOptions(timeProvider.GetUtcNow().UtcDateTime))
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        if (backupService is not null)
        {
            await backupService.CreateAutomaticIfNeededAsync(cancellationToken);
        }
    }
}
