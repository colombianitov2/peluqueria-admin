namespace PeluqueriaAdmin.Domain.Settings;

public readonly record struct CurrencyCode
{
    private CurrencyCode(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static CurrencyCode From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalizedValue = value.Trim().ToUpperInvariant();
        if (normalizedValue.Length != 3 || !normalizedValue.All(char.IsAsciiLetter))
        {
            throw new ArgumentException(
                "El código de moneda debe contener exactamente tres letras.",
                nameof(value));
        }

        return new CurrencyCode(normalizedValue);
    }

    public override string ToString() => Value;
}
