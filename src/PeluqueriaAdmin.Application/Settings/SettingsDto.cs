namespace PeluqueriaAdmin.Application.Settings;

public sealed record SettingsDto(
    decimal? WeeklyUsageFee,
    decimal? CollaboratorProfitPercent,
    int TotalChairs,
    string CurrencyCode,
    string ExportDirectory,
    DateTime CreatedUtc,
    DateTime UpdatedUtc)
{
    public decimal? DailyUsageFee => WeeklyUsageFee;

    public decimal RequireDailyUsageFee() =>
        DailyUsageFee
        ?? throw new InvalidOperationException(
            "Sin configurar: define la Tarifa diaria por uso del local (USD) en Ajustes.");

    public decimal RequireCollaboratorProfitPercent() =>
        CollaboratorProfitPercent
        ?? throw new InvalidOperationException(
            "Sin configurar: define la Ganancia colaboradores (%) en Ajustes.");
}
