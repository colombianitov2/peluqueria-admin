namespace PeluqueriaAdmin.Application.Settings;

public sealed record SettingsDto(
    decimal WeeklyUsageFee,
    decimal CollaboratorProfitPercent,
    decimal OptionalSuppliesMonthlyBudget,
    int TotalChairs,
    string CurrencyCode,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
