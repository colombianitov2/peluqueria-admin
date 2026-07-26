using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Settings;

internal static class SettingsMapper
{
    public static SettingsDto ToDto(GeneralSettings settings) => new(
        settings.WeeklyUsageFee.ToDecimal(),
        settings.CollaboratorProfit.ToPercent(),
        settings.TotalChairs,
        ApplicationCurrency.Code,
        settings.ExportDirectory,
        settings.CreatedUtc,
        settings.UpdatedUtc);
}
