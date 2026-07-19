namespace PeluqueriaAdmin.Domain.Common;

public readonly record struct YearMonth
{
    public YearMonth(int year, int month)
    {
        if (year is < 1 or > 9999)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month));
        }

        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public DateOnly FirstDay => new(Year, Month, 1);

    public DateOnly LastDay => new(Year, Month, DateTime.DaysInMonth(Year, Month));

    public static YearMonth From(DateOnly date) => new(date.Year, date.Month);

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
