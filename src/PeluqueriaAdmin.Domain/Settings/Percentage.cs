namespace PeluqueriaAdmin.Domain.Settings;

public readonly record struct Percentage
{
    public const int MaximumBasisPoints = 10_000;
    private const decimal BasisPointsPerPercent = 100m;

    private Percentage(int basisPoints)
    {
        BasisPoints = basisPoints;
    }

    public int BasisPoints { get; }

    public static Percentage FromPercent(decimal percent)
    {
        if (percent < 0m || percent > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(percent), "El porcentaje debe estar entre 0 y 100.");
        }

        decimal scaledPercent = percent * BasisPointsPerPercent;
        if (scaledPercent != decimal.Truncate(scaledPercent))
        {
            throw new ArgumentException("El porcentaje no puede tener más de dos decimales.", nameof(percent));
        }

        return new Percentage((int)scaledPercent);
    }

    public static Percentage FromBasisPoints(int basisPoints)
    {
        if (basisPoints < 0 || basisPoints > MaximumBasisPoints)
        {
            throw new ArgumentOutOfRangeException(
                nameof(basisPoints),
                $"Los puntos básicos deben estar entre 0 y {MaximumBasisPoints}.");
        }

        return new Percentage(basisPoints);
    }

    public decimal ToPercent() => BasisPoints / BasisPointsPerPercent;
}
