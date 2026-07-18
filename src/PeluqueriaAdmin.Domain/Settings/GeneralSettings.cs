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
            CurrencyCode = CurrencyCode.From("USD"),
            CreatedUtc = utcNow,
            UpdatedUtc = utcNow,
        };
    }

    public void Update(
        Money weeklyUsageFee,
        Percentage collaboratorProfit,
        Money optionalSuppliesMonthlyBudget,
        int totalChairs,
        CurrencyCode currencyCode,
        DateTime utcNow)
    {
        if (totalChairs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalChairs), "La cantidad de sillas no puede ser negativa.");
        }

        EnsureUtc(utcNow);

        WeeklyUsageFee = weeklyUsageFee;
        CollaboratorProfit = collaboratorProfit;
        OptionalSuppliesMonthlyBudget = optionalSuppliesMonthlyBudget;
        TotalChairs = totalChairs;
        CurrencyCode = currencyCode;
        UpdatedUtc = utcNow;
    }

    private static void EnsureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("La fecha debe estar expresada en UTC.", nameof(value));
        }
    }
}
