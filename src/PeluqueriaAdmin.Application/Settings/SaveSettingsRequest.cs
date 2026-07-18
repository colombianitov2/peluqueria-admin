namespace PeluqueriaAdmin.Application.Settings;

public sealed record SaveSettingsRequest(
    decimal WeeklyUsageFee,
    decimal CollaboratorProfitPercent,
    decimal OptionalSuppliesMonthlyBudget,
    int TotalChairs,
    string CurrencyCode);
