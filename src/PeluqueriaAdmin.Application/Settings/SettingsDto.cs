namespace PeluqueriaAdmin.Application.Settings;

public sealed record SettingsDto(
    decimal WeeklyUsageFee,
    decimal CollaboratorProfitPercent,
    int TotalChairs,
    string CurrencyCode,
    string ExportDirectory,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
