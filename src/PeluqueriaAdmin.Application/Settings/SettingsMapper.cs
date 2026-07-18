using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Settings;

internal static class SettingsMapper
{
    public static SettingsDto ToDto(GeneralSettings settings) => new(
        settings.WeeklyUsageFee.ToDecimal(),
        settings.CollaboratorProfit.ToPercent(),
        settings.OptionalSuppliesMonthlyBudget.ToDecimal(),
        settings.TotalChairs,
        settings.CurrencyCode.Value,
        settings.CreatedUtc,
        settings.UpdatedUtc);
}
