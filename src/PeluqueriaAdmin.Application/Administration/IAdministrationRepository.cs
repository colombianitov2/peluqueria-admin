using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Administration;

public interface IAdministrationRepository
{
    Task<AdministrationData> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyCollection<AuditableEntity> additions,
        IReadOnlyCollection<AuditableEntity> updates,
        CancellationToken cancellationToken = default);

    Task SaveCompletingDraftAsync(
        IReadOnlyCollection<AuditableEntity> additions,
        IReadOnlyCollection<AuditableEntity> updates,
        string completedDraftKey,
        CancellationToken cancellationToken = default);

    Task SaveSettingsAndRateAsync(
        GeneralSettings settings,
        WeeklyRate? newRate,
        CancellationToken cancellationToken = default);

    Task SaveSettingsAndRateCompletingDraftAsync(
        GeneralSettings settings,
        WeeklyRate? newRate,
        string completedDraftKey,
        CancellationToken cancellationToken = default);
}
