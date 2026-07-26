using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class ObligationCreditWeeklyTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void PersistedEnumValues_PreserveExistingValuesAndAppendCreditAndWeekly()
    {
        Assert.Equal(1, (int)ObligationType.Service);
        Assert.Equal(2, (int)ObligationType.Tax);
        Assert.Equal(3, (int)ObligationType.OtherRecurring);
        Assert.Equal(4, (int)ObligationType.Credit);
        Assert.Equal(0, (int)RecurrenceFrequency.None);
        Assert.Equal(1, (int)RecurrenceFrequency.Monthly);
        Assert.Equal(2, (int)RecurrenceFrequency.Annual);
        Assert.Equal(3, (int)RecurrenceFrequency.Weekly);
    }

    [Fact]
    public void WeeklyRecurrence_UsesSevenDayAnchorAndIsIdempotentAcrossMonthBoundary()
    {
        Obligation template = Obligation.Create(
            "Crédito semanal",
            ObligationType.Credit,
            new DateOnly(2026, 7, 23),
            Money.FromDecimal(25m),
            RecurrenceFrequency.Weekly,
            UtcNow);

        IReadOnlyList<Obligation> generated = ObligationRecurrenceGenerator.Generate(
            template,
            [template],
            new DateOnly(2026, 8, 13),
            UtcNow);
        IReadOnlyList<Obligation> repeated = ObligationRecurrenceGenerator.Generate(
            template,
            new[] { template }.Concat(generated),
            new DateOnly(2026, 8, 13),
            UtcNow);

        Assert.Equal(
            [
                new DateOnly(2026, 7, 30),
                new DateOnly(2026, 8, 6),
                new DateOnly(2026, 8, 13),
            ],
            generated.Select(item => item.DueDate));
        Assert.All(generated, item => Assert.Equal(template.SeriesId, item.SeriesId));
        Assert.Empty(repeated);
    }

    [Fact]
    public void SettledObligation_UsesFinalActualAmountAndHasNoPendingBalance()
    {
        Obligation obligation = Obligation.Create(
            "Crédito liquidado",
            ObligationType.Credit,
            new DateOnly(2026, 7, 23),
            Money.FromDecimal(100m),
            RecurrenceFrequency.None,
            UtcNow);
        ObligationPayment payment = ObligationPayment.Create(
            obligation.Id,
            new DateOnly(2026, 7, 23),
            Money.FromDecimal(80m),
            UtcNow);

        Assert.Equal(10_000, obligation.GoalAmount([payment]).MinorUnits);
        Assert.Equal(2_000, obligation.OutstandingAmount([payment]).MinorUnits);

        obligation.MarkSettled(UtcNow.AddMinutes(1));

        Assert.Equal(8_000, obligation.GoalAmount([payment]).MinorUnits);
        Assert.Equal(0, obligation.OutstandingAmount([payment]).MinorUnits);
        Assert.Equal(
            ObligationStatus.Paid,
            obligation.Status([payment], new DateOnly(2026, 7, 24)));
    }
}
