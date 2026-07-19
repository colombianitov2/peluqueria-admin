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
        Money weeklyUsageFee = Money.FromDecimal(request.WeeklyUsageFee);
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        WeeklyRate? newRate = settings.WeeklyUsageFee == weeklyUsageFee
            ? null
            : WeeklyRate.Create(DateOnly.FromDateTime(utcNow), weeklyUsageFee, utcNow);
        settings.Update(
            weeklyUsageFee,
            Percentage.FromPercent(request.CollaboratorProfitPercent),
            Money.FromDecimal(request.OptionalSuppliesMonthlyBudget),
            settings.TotalChairs,
            CurrencyCode.From(request.CurrencyCode),
            utcNow);

        if (string.IsNullOrWhiteSpace(completedDraftKey))
        {
            await administrationRepository.SaveSettingsAndRateAsync(settings, newRate, cancellationToken);
        }
        else
        {
            await administrationRepository.SaveSettingsAndRateCompletingDraftAsync(
                settings, newRate, completedDraftKey, cancellationToken);
        }
        return SettingsMapper.ToDto(settings);
    }
}
