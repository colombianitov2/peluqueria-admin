namespace PeluqueriaAdmin.Application.Settings;

public sealed record SettingsDto(
    decimal? WeeklyUsageFee,
    decimal CollaboratorProfitPercent,
    int TotalChairs,
    string CurrencyCode,
    string ExportDirectory,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    bool IsDailyUsageFeeConfirmed = true)
{
    public decimal? DailyUsageFee => WeeklyUsageFee;

    public bool IsDailyUsageFeePendingConfirmation =>
        WeeklyUsageFee.HasValue && !IsDailyUsageFeeConfirmed;
}
