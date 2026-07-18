namespace PeluqueriaAdmin.Domain.Inventory;

public readonly record struct Quantity
{
    private const int MaximumDecimals = 3;

    private Quantity(decimal value)
    {
        Value = value;
    }

    public decimal Value { get; }

    public static Quantity Positive(decimal value)
    {
        if (value <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "La cantidad debe ser mayor que cero.");
        }

        EnsurePrecision(value);
        return new Quantity(value);
    }

    public static Quantity NonNegative(decimal value)
    {
        if (value < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "La cantidad no puede ser negativa.");
        }

        EnsurePrecision(value);
        return new Quantity(value);
    }

    private static void EnsurePrecision(decimal value)
    {
        decimal factor = 1_000m;
        if (value * factor != decimal.Truncate(value * factor))
        {
            throw new ArgumentException($"La cantidad no puede tener más de {MaximumDecimals} decimales.", nameof(value));
        }
    }
}
