using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Settings;

public sealed class SaveSettingsUseCase(
    ISettingsRepository repository,
    IAdministrationRepository administrationRepository,
    TimeProvider timeProvider)
{
    public async Task<SettingsDto> ExecuteAsync(
        SaveSettingsRequest request,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        GeneralSettings settings = await repository.GetAsync(cancellationToken);
        Money? dailyUsageFee = request.DailyUsageFee.HasValue
            ? Money.FromDecimal(request.DailyUsageFee.Value)
            : null;
        Percentage? collaboratorProfit = request.CollaboratorProfitPercent.HasValue
            ? Percentage.FromPercent(request.CollaboratorProfitPercent.Value)
            : null;
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        DailyRate? newRate = !dailyUsageFee.HasValue || settings.DailyUsageFee == dailyUsageFee
            ? null
            : DailyRate.Create(
                DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
                utcNow,
                dailyUsageFee.Value,
                utcNow);
        settings.Update(
            dailyUsageFee,
            collaboratorProfit,
            settings.TotalChairs,
            request.ExportDirectory,
            utcNow);

        if (string.IsNullOrWhiteSpace(completedDraftKey))
        {
            await administrationRepository.SaveSettingsAndDailyRateAsync(settings, newRate, cancellationToken);
        }
        else
        {
            await administrationRepository.SaveSettingsAndDailyRateCompletingDraftAsync(
                settings, newRate, completedDraftKey, cancellationToken);
        }
        return SettingsMapper.ToDto(settings);
    }
}
