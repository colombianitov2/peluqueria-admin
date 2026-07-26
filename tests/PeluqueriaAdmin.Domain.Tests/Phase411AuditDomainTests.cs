using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class Phase411AuditDomainTests
{
    private static readonly DateTime Utc =
        new(2026, 7, 24, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SundayEntry_IsFullAndTuesdayEntry_IsFiveSevenths()
    {
        DateOnly sunday = new(2026, 7, 19);
        DateOnly tuesday = new(2026, 7, 21);
        WeeklyRate rate = WeeklyRate.Create(
            sunday,
            Money.FromDecimal(12m),
            Utc);
        LocalUsePerson sundayWorker = LocalUsePerson.Create(
            "Domingo",
            sunday,
            null,
            Utc);
        LocalUsePerson tuesdayWorker = LocalUsePerson.Create(
            "Martes",
            tuesday,
            null,
            Utc);

        WeeklyCharge sundayCharge = Assert.Single(
            WeeklyChargeCalculator.Generate(
                sundayWorker,
                [],
                [rate],
                new DateOnly(2026, 7, 25),
                Utc));
        WeeklyCharge tuesdayCharge = Assert.Single(
            WeeklyChargeCalculator.Generate(
                tuesdayWorker,
                [],
                [rate],
                new DateOnly(2026, 7, 25),
                Utc));

        Assert.Equal(1_200, sundayCharge.Amount.MinorUnits);
        Assert.Equal(857, tuesdayCharge.Amount.MinorUnits);
        Assert.Equal(sundayCharge.DueDate, tuesdayCharge.DueDate);
    }

    [Fact]
    public void UnassignedCollaboratorFundReturnsToRetainedLocalResult()
    {
        MonthlySummaryResult summary =
            new(100_000, 0, 0, 100_000, 20_000, 80_000);
        MonthlyClose close = MonthlyClose.Create(
            new YearMonth(2026, 7),
            Percentage.FromPercent(20m),
            summary,
            Utc);

        IReadOnlyList<MonthlyCloseParticipant> participants =
            CollaboratorDistributionCalculator.Distribute(
                close,
                [(Guid.NewGuid(), 6_000)],
                Utc.AddMinutes(1));

        Assert.Equal(
            12_000,
            Assert.Single(participants).Amount.MinorUnits);
        Assert.Equal(12_000, close.FundMinorUnits);
        Assert.Equal(88_000, close.RetainedResultMinorUnits);
        Assert.Equal(
            100_000,
            close.FundMinorUnits + close.RetainedResultMinorUnits);
    }
}
