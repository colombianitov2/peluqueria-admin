using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Tests;

internal sealed class ConfiguredDatabaseInitializer(
    IDbContextFactory<PeluqueriaDbContext> contextFactory,
    ApplicationPaths applicationPaths,
    TimeProvider timeProvider,
    DatabaseBackupService? backupService = null)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await new DatabaseInitializer(
            contextFactory,
            applicationPaths,
            timeProvider,
            backupService).InitializeAsync(cancellationToken);

        await using PeluqueriaDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        GeneralSettings settings = await context.Settings.SingleAsync(cancellationToken);
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        Money dailyRate = Money.FromDecimal(12m);
        settings.Update(
            dailyRate,
            Percentage.FromPercent(20m),
            settings.TotalChairs,
            settings.ExportDirectory,
            utcNow);
        context.Settings.Update(settings);
        if (!await context.DailyRates.AnyAsync(cancellationToken))
        {
            context.DailyRates.Add(DailyRate.Create(
                DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
                utcNow,
                dailyRate,
                utcNow));
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
