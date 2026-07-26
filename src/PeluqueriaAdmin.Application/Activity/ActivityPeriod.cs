namespace PeluqueriaAdmin.Application.Activity;

public enum ActivityPeriod
{
    Today = 1,
    ThisWeek = 2,
    ThisMonth = 3,
    LastThreeMonths = 4,
    LastSixMonths = 5,
    ThisYear = 6,
    Custom = 7,
}

public readonly record struct ActivityDateRange(DateOnly From, DateOnly Through)
{
    public bool Contains(DateOnly value) => value >= From && value <= Through;
}

public static class ActivityPeriodCalculator
{
    public static ActivityDateRange Calculate(
        ActivityPeriod period,
        DateOnly today,
        DateOnly? customFrom = null,
        DateOnly? customThrough = null)
    {
        return period switch
        {
            ActivityPeriod.Today => new(today, today),
            ActivityPeriod.ThisWeek => new(StartOfWeek(today), StartOfWeek(today).AddDays(6)),
            ActivityPeriod.ThisMonth => new(new DateOnly(today.Year, today.Month, 1),
                new DateOnly(today.Year, today.Month, 1).AddMonths(1).AddDays(-1)),
            ActivityPeriod.LastThreeMonths => new(
                new DateOnly(today.Year, today.Month, 1).AddMonths(-2), today),
            ActivityPeriod.LastSixMonths => new(
                new DateOnly(today.Year, today.Month, 1).AddMonths(-5), today),
            ActivityPeriod.ThisYear => new(new DateOnly(today.Year, 1, 1), today),
            ActivityPeriod.Custom => Custom(customFrom, customThrough),
            _ => throw new ArgumentOutOfRangeException(nameof(period)),
        };
    }

    private static DateOnly StartOfWeek(DateOnly date)
    {
        int offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset);
    }

    private static ActivityDateRange Custom(DateOnly? from, DateOnly? through)
    {
        if (!from.HasValue || !through.HasValue || from.Value > through.Value)
        {
            throw new ArgumentException("El rango personalizado requiere fechas válidas y ordenadas.");
        }

        return new ActivityDateRange(from.Value, through.Value);
    }
}
