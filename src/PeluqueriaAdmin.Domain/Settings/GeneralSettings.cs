namespace PeluqueriaAdmin.Domain.Settings;

public sealed class GeneralSettings
{
    public const int SingletonId = 1;

    private GeneralSettings()
    {
    }

    public int Id { get; private set; }

    public Money WeeklyUsageFee { get; private set; }

    public Percentage CollaboratorProfit { get; private set; }

    public Money OptionalSuppliesMonthlyBudget { get; private set; }

    public int TotalChairs { get; private set; }

    public CurrencyCode CurrencyCode { get; private set; }

    public string ExportDirectory { get; private set; } = string.Empty;

    public DateTime CreatedUtc { get; private set; }

    public DateTime UpdatedUtc { get; private set; }

    public static GeneralSettings CreateDefault(DateTime utcNow)
    {
        EnsureUtc(utcNow);

        return new GeneralSettings
        {
            Id = SingletonId,
            WeeklyUsageFee = Money.FromDecimal(12.00m),
            CollaboratorProfit = Percentage.FromPercent(20.00m),
            OptionalSuppliesMonthlyBudget = Money.FromDecimal(0.00m),
            TotalChairs = 0,
            CurrencyCode = CurrencyCode.From(ApplicationCurrency.Code),
            ExportDirectory = string.Empty,
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow,
        };
    }

    public void Update(
        Money weeklyUsageFee,
        Percentage collaboratorProfit,
        int totalChairs,
        string? exportDirectory,
        DateTime utcNow)
    {
        if (totalChairs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalChairs), "La cantidad de sillas no puede ser negativa.");
        }

        EnsureUtc(utcNow);

        string normalizedExportDirectory = exportDirectory?.Trim() ?? string.Empty;
        if (normalizedExportDirectory.Length > 1024)
        {
            throw new ArgumentException("La carpeta de exportación es demasiado larga.", nameof(exportDirectory));
        }

        WeeklyUsageFee = weeklyUsageFee;
        CollaboratorProfit = collaboratorProfit;
        OptionalSuppliesMonthlyBudget = Money.FromMinorUnits(0);
        TotalChairs = totalChairs;
        CurrencyCode = CurrencyCode.From(ApplicationCurrency.Code);
        ExportDirectory = normalizedExportDirectory;
        UpdatedUtc = utcNow;
    }

    public void Update(
        Money weeklyUsageFee,
        Percentage collaboratorProfit,
        Money retiredOptionalSuppliesMonthlyBudget,
        int totalChairs,
        CurrencyCode retiredCurrencyCode,
        DateTime utcNow)
    {
        _ = retiredOptionalSuppliesMonthlyBudget;
        _ = retiredCurrencyCode;
        Update(weeklyUsageFee, collaboratorProfit, totalChairs, ExportDirectory, utcNow);
    }

    public bool NormalizeRetiredOptions(DateTime utcNow)
    {
        EnsureUtc(utcNow);
        bool changed = OptionalSuppliesMonthlyBudget.MinorUnits != 0
            || CurrencyCode.Value != ApplicationCurrency.Code;
        if (!changed)
        {
            return false;
        }

        OptionalSuppliesMonthlyBudget = Money.FromMinorUnits(0);
        CurrencyCode = CurrencyCode.From(ApplicationCurrency.Code);
        UpdatedUtc = utcNow;
        return true;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("La fecha debe estar expresada en UTC.", nameof(value));
        }
    }
}
