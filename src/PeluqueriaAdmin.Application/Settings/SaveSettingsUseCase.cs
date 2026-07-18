using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Settings;

public sealed class SaveSettingsUseCase(
    ISettingsRepository repository,
    TimeProvider timeProvider)
{
    public async Task<SettingsDto> ExecuteAsync(
        SaveSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        GeneralSettings settings = await repository.GetAsync(cancellationToken);
        settings.Update(
            Money.FromDecimal(request.WeeklyUsageFee),
            Percentage.FromPercent(request.CollaboratorProfitPercent),
            Money.FromDecimal(request.OptionalSuppliesMonthlyBudget),
            request.TotalChairs,
            CurrencyCode.From(request.CurrencyCode),
            timeProvider.GetUtcNow().UtcDateTime);

        await repository.SaveAsync(settings, cancellationToken);
        return SettingsMapper.ToDto(settings);
    }
}
