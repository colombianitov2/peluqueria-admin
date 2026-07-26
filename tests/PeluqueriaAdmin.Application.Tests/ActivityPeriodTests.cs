using PeluqueriaAdmin.Application.Activity;

namespace PeluqueriaAdmin.Application.Tests;

public sealed class ActivityPeriodTests
{
    private static readonly DateOnly Today = new(2026, 7, 19);

    [Theory]
    [InlineData(ActivityPeriod.Today, "2026-07-19", "2026-07-19")]
    [InlineData(ActivityPeriod.ThisWeek, "2026-07-13", "2026-07-19")]
    [InlineData(ActivityPeriod.ThisMonth, "2026-07-01", "2026-07-31")]
    [InlineData(ActivityPeriod.LastThreeMonths, "2026-05-01", "2026-07-19")]
    [InlineData(ActivityPeriod.LastSixMonths, "2026-02-01", "2026-07-19")]
    [InlineData(ActivityPeriod.ThisYear, "2026-01-01", "2026-07-19")]
    public void StandardPeriods_ReturnInclusiveExpectedRange(ActivityPeriod period, string from, string through)
    {
        ActivityDateRange range = ActivityPeriodCalculator.Calculate(period, Today);

        Assert.Equal(DateOnly.Parse(from), range.From);
        Assert.Equal(DateOnly.Parse(through), range.Through);
        Assert.True(range.Contains(Today));
    }

    [Fact]
    public void CustomPeriod_RequiresAnOrderedCompleteRange()
    {
        ActivityDateRange range = ActivityPeriodCalculator.Calculate(
            ActivityPeriod.Custom, Today, new DateOnly(2025, 1, 1), new DateOnly(2025, 2, 1));

        Assert.Equal(new DateOnly(2025, 1, 1), range.From);
        Assert.Throws<ArgumentException>(() => ActivityPeriodCalculator.Calculate(
            ActivityPeriod.Custom, Today, Today, Today.AddDays(-1)));
    }
}
