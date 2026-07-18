using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Settings;

public interface ISettingsRepository
{
    Task<GeneralSettings> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(GeneralSettings settings, CancellationToken cancellationToken = default);
}
