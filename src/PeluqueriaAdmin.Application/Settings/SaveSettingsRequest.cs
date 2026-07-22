namespace PeluqueriaAdmin.Application.Settings;

public sealed record SaveSettingsRequest(
    decimal WeeklyUsageFee,
    decimal CollaboratorProfitPercent,
    string ExportDirectory)
{
    public SaveSettingsRequest(
        decimal weeklyUsageFee,
        decimal collaboratorProfitPercent,
        decimal retiredOptionalSuppliesMonthlyBudget,
        int retiredTotalChairs,
        string retiredCurrencyCode)
        : this(weeklyUsageFee, collaboratorProfitPercent, string.Empty)
    {
        _ = retiredOptionalSuppliesMonthlyBudget;
        _ = retiredTotalChairs;
        _ = retiredCurrencyCode;
    }
}
