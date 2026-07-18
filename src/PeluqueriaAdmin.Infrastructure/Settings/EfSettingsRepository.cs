using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Persistence;

namespace PeluqueriaAdmin.Infrastructure.Settings;

public sealed class EfSettingsRepository(IDbContextFactory<PeluqueriaDbContext> contextFactory)
    : ISettingsRepository
{
    public async Task<GeneralSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Settings
            .SingleOrDefaultAsync(settings => settings.Id == GeneralSettings.SingletonId, cancellationToken)
            ?? throw new InvalidOperationException("No existe la configuración general del programa.");
    }

    public async Task SaveAsync(
        GeneralSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Id != GeneralSettings.SingletonId)
        {
            throw new InvalidOperationException("La configuración general tiene un identificador inválido.");
        }

        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        context.Settings.Update(settings);
        await context.SaveChangesAsync(cancellationToken);
    }
}
