namespace PeluqueriaAdmin.Application.Settings;

public sealed record SaveSettingsRequest(
    decimal WeeklyUsageFee,
    decimal CollaboratorProfitPercent,
    decimal OptionalSuppliesMonthlyBudget,
    string CurrencyCode)
{
    public SaveSettingsRequest(
        decimal weeklyUsageFee,
        decimal collaboratorProfitPercent,
        decimal optionalSuppliesMonthlyBudget,
        int legacyTotalChairs,
        string currencyCode)
        : this(weeklyUsageFee, collaboratorProfitPercent, optionalSuppliesMonthlyBudget, currencyCode)
    {
        _ = legacyTotalChairs;
    }
}
