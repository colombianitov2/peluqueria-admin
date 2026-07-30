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
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        bool pendingUnchanged = settings.IsDailyUsageFeePendingConfirmation
            && settings.DailyUsageFee == dailyUsageFee
            && !request.ConfirmDailyUsageFee;
        bool rateChanged = !pendingUnchanged
            && (settings.DailyUsageFee != dailyUsageFee
                || settings.IsDailyUsageFeeConfirmed != dailyUsageFee.HasValue);
        DailyRate? newRate = rateChanged
            ? DailyRate.Create(
                DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
                utcNow,
                dailyUsageFee,
                utcNow)
            : null;
        bool isConfirmed = pendingUnchanged
            ? false
            : dailyUsageFee.HasValue;
        settings.Update(
            dailyUsageFee,
            isConfirmed,
            Percentage.FromPercent(request.CollaboratorProfitPercent),
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
