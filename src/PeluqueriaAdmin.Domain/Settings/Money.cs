namespace PeluqueriaAdmin.Domain.Settings;

public readonly record struct Money
{
    private const decimal MinorUnitsPerUnit = 100m;

    private Money(long minorUnits)
    {
        MinorUnits = minorUnits;
    }

    public long MinorUnits { get; }

    public static Money FromDecimal(decimal amount)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "El valor monetario no puede ser negativo.");
        }

        decimal scaledAmount = checked(amount * MinorUnitsPerUnit);
        if (scaledAmount != decimal.Truncate(scaledAmount))
        {
            throw new ArgumentException("El valor monetario no puede tener más de dos decimales.", nameof(amount));
        }

        if (scaledAmount > long.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "El valor monetario es demasiado grande.");
        }

        return new Money((long)scaledAmount);
    }

    public static Money FromMinorUnits(long minorUnits)
    {
        if (minorUnits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minorUnits), "Las unidades menores no pueden ser negativas.");
        }

        return new Money(minorUnits);
    }

    public decimal ToDecimal() => MinorUnits / MinorUnitsPerUnit;
}
