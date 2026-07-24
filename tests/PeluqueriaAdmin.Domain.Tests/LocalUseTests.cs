using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class LocalUseTests
{
    private static readonly DateTime UtcNow =
        new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, 1200)]
    [InlineData(1, 1029)]
    [InlineData(2, 857)]
    [InlineData(3, 686)]
    [InlineData(4, 514)]
    [InlineData(5, 343)]
    [InlineData(6, 171)]
    public void FirstCharge_IsProratedFromEntryDayThroughSaturdayInclusive(
        int daysAfterSunday,
        long expectedMinorUnits)
    {
        DateOnly sunday = new(2026, 7, 19);
        DateOnly entry = sunday.AddDays(daysAfterSunday);
        LocalUsePerson person = LocalUsePerson.Create(
            "Ana",
            entry,
            null,
            UtcNow);
        WeeklyRate rate = WeeklyRate.Create(
            sunday,
            Money.FromDecimal(12m),
            UtcNow);

        WeeklyCharge charge = Assert.Single(
            WeeklyChargeCalculator.Generate(
                person,
                [],
                [rate],
                new DateOnly(2026, 7, 25),
                UtcNow));

        Assert.Equal(entry, charge.PeriodStart);
        Assert.Equal(new DateOnly(2026, 7, 25), charge.PeriodEnd);
        Assert.Equal(new DateOnly(2026, 7, 25), charge.DueDate);
        Assert.Equal(expectedMinorUnits, charge.Amount.MinorUnits);
    }

    [Fact]
    public void SundayEntry_PaysTheFullWeeklyRateOnTheImmediateSaturday()
    {
        DateOnly entry = new(2026, 7, 19);
        LocalUsePerson person = LocalUsePerson.Create(
            "Domingo",
            entry,
            null,
            UtcNow);
        WeeklyRate rate = WeeklyRate.Create(
            entry,
            Money.FromDecimal(12m),
            UtcNow);

        WeeklyCharge charge = Assert.Single(
            WeeklyChargeCalculator.Generate(
                person,
                [],
                [rate],
                new DateOnly(2026, 7, 25),
                UtcNow));

        Assert.Equal(1_200, charge.Amount.MinorUnits);
        Assert.Equal(new DateOnly(2026, 7, 25), charge.DueDate);
    }

    [Fact]
    public void TuesdayEntry_PaysFiveSeventhsOnTheImmediateSaturday()
    {
        DateOnly entry = new(2026, 7, 21);
        LocalUsePerson person = LocalUsePerson.Create(
            "Martes",
            entry,
            null,
            UtcNow);
        WeeklyRate rate = WeeklyRate.Create(
            entry,
            Money.FromDecimal(12m),
            UtcNow);

        WeeklyCharge charge = Assert.Single(
            WeeklyChargeCalculator.Generate(
                person,
                [],
                [rate],
                new DateOnly(2026, 7, 25),
                UtcNow));

        Assert.Equal(857, charge.Amount.MinorUnits);
    }

    [Fact]
    public void ChargesAfterTheFirstSaturday_UseTheFullWeeklyRate()
    {
        DateOnly entry = new(2026, 7, 21);
        LocalUsePerson person = LocalUsePerson.Create(
            "Ana",
            entry,
            null,
            UtcNow);
        WeeklyRate rate = WeeklyRate.Create(
            entry,
            Money.FromDecimal(12m),
            UtcNow);

        IReadOnlyList<WeeklyCharge> charges =
            WeeklyChargeCalculator.Generate(
                person,
                [],
                [rate],
                new DateOnly(2026, 8, 1),
                UtcNow);

        Assert.Collection(
            charges,
            first =>
            {
                Assert.Equal(entry, first.PeriodStart);
                Assert.Equal(857, first.Amount.MinorUnits);
            },
            second =>
            {
                Assert.Equal(new DateOnly(2026, 7, 26), second.PeriodStart);
                Assert.Equal(1_200, second.Amount.MinorUnits);
            });
    }

    [Fact]
    public void RateChangesApplyByPeriodStartWithoutRewritingHistory()
    {
        DateOnly entry = new(2026, 7, 21);
        LocalUsePerson person = LocalUsePerson.Create(
            "Luis",
            entry,
            null,
            UtcNow);
        WeeklyRate original = WeeklyRate.Create(
            entry,
            Money.FromDecimal(12m),
            UtcNow);
        WeeklyRate changed = WeeklyRate.Create(
            new DateOnly(2026, 7, 27),
            Money.FromDecimal(20m),
            UtcNow.AddMinutes(1));

        IReadOnlyList<WeeklyCharge> charges =
            WeeklyChargeCalculator.Generate(
                person,
                [],
                [original, changed],
                new DateOnly(2026, 8, 8),
                UtcNow);

        Assert.Equal([857L, 1_200L, 2_000L],
            charges.Select(item => item.Amount.MinorUnits));
    }

    [Fact]
    public void Generate_DoesNotDuplicateAnExistingSaturdayCharge()
    {
        DateOnly entry = new(2026, 7, 21);
        LocalUsePerson person = LocalUsePerson.Create(
            "Ana",
            entry,
            null,
            UtcNow);
        WeeklyRate rate = WeeklyRate.Create(
            entry,
            Money.FromDecimal(12m),
            UtcNow);
        WeeklyCharge first = Assert.Single(
            WeeklyChargeCalculator.Generate(
                person,
                [],
                [rate],
                new DateOnly(2026, 7, 25),
                UtcNow));

        Assert.Empty(WeeklyChargeCalculator.Generate(
            person,
            [first],
            [rate],
            new DateOnly(2026, 7, 25),
            UtcNow.AddMinutes(1)));
    }

    [Fact]
    public void HistoricalTuesdayEntry_OwesProratedFirstWeekPlusFullWeeks()
    {
        DateOnly today = new(2026, 7, 20);
        DateOnly entry = new(2026, 6, 16);
        LocalUsePerson person = LocalUsePerson.Create(
            "Histórico",
            entry,
            null,
            UtcNow);
        WeeklyRate rate = WeeklyRate.Create(
            entry,
            Money.FromDecimal(12m),
            UtcNow);

        IReadOnlyList<WeeklyCharge> charges =
            WeeklyChargeCalculator.Generate(
                person,
                [],
                [rate],
                today,
                UtcNow);

        Assert.Equal(5, charges.Count);
        Assert.Equal(
            5_657,
            WeeklyChargeCalculator.CalculateDebt(
                charges,
                [],
                today).MinorUnits);
    }

    [Fact]
    public void Account_ProjectsSundayAdvanceAcrossFullSaturdayCharges()
    {
        DateOnly entry = new(2026, 7, 19);
        LocalUsePerson person = LocalUsePerson.Create(
            "Sara",
            entry,
            null,
            UtcNow);
        WeeklyRate rate = WeeklyRate.Create(
            entry,
            Money.FromDecimal(12m),
            UtcNow);
        LocalUsePayment payment = LocalUsePayment.Create(
            person.Id,
            entry,
            Money.FromDecimal(24m),
            UtcNow);

        WorkerAccountBalance balance =
            WeeklyChargeCalculator.CalculateAccount(
                person,
                [],
                [payment],
                [rate],
                entry);

        Assert.Equal(new DateOnly(2026, 7, 25), balance.NextChargeDate);
        Assert.Equal(1_200, balance.NextChargeAmount?.MinorUnits);
        Assert.Equal(new DateOnly(2026, 8, 8),
            balance.NextRequiredPaymentDate);
        Assert.Equal(1_200,
            balance.NextRequiredPaymentAmount?.MinorUnits);
        Assert.Equal(new DateOnly(2026, 8, 1),
            balance.CoveredThroughDate);
    }

    [Fact]
    public void Account_ProjectsMondayProrationBeforeFullWeeks()
    {
        DateOnly entry = new(2026, 7, 20);
        LocalUsePerson person = LocalUsePerson.Create(
            "Lunes",
            entry,
            null,
            UtcNow);
        WeeklyRate rate = WeeklyRate.Create(
            entry,
            Money.FromDecimal(12m),
            UtcNow);
        LocalUsePayment payment = LocalUsePayment.Create(
            person.Id,
            entry,
            Money.FromDecimal(24m),
            UtcNow);

        WorkerAccountBalance balance =
            WeeklyChargeCalculator.CalculateAccount(
                person,
                [],
                [payment],
                [rate],
                entry);

        Assert.Equal(1_029, balance.NextChargeAmount?.MinorUnits);
        Assert.Equal(new DateOnly(2026, 8, 8),
            balance.NextRequiredPaymentDate);
        Assert.Equal(1_029,
            balance.NextRequiredPaymentAmount?.MinorUnits);
        Assert.Equal(new DateOnly(2026, 8, 1),
            balance.CoveredThroughDate);
    }

    [Fact]
    public void PaymentsRemainIndependentForEachWorker()
    {
        DateOnly entry = new(2026, 7, 21);
        LocalUsePerson first = LocalUsePerson.Create(
            "Ana",
            entry,
            null,
            UtcNow);
        LocalUsePerson second = LocalUsePerson.Create(
            "Beto",
            entry,
            null,
            UtcNow);
        WeeklyRate rate = WeeklyRate.Create(
            entry,
            Money.FromDecimal(12m),
            UtcNow);
        LocalUsePayment firstPayment = LocalUsePayment.Create(
            first.Id,
            entry,
            Money.FromDecimal(5m),
            UtcNow);
        LocalUsePayment secondPayment = LocalUsePayment.Create(
            second.Id,
            entry,
            Money.FromDecimal(100m),
            UtcNow);

        WorkerAccountBalance firstBalance =
            WeeklyChargeCalculator.CalculateAccount(
                first,
                [],
                [firstPayment, secondPayment],
                [rate],
                entry);
        WorkerAccountBalance secondBalance =
            WeeklyChargeCalculator.CalculateAccount(
                second,
                [],
                [firstPayment, secondPayment],
                [rate],
                entry);

        Assert.Equal(500, firstBalance.Credit.MinorUnits);
        Assert.Equal(10_000, secondBalance.Credit.MinorUnits);
    }

    [Fact]
    public void ExitAfterSaturday_KeepsThatSaturdayChargeAndStopsLaterOnes()
    {
        DateOnly entry = new(2026, 7, 21);
        DateOnly exit = new(2026, 7, 26);
        LocalUsePerson person = LocalUsePerson.Create(
            "Nora",
            entry,
            exit,
            UtcNow);
        WeeklyRate rate = WeeklyRate.Create(
            entry,
            Money.FromDecimal(12m),
            UtcNow);

        WeeklyCharge charge = Assert.Single(
            WeeklyChargeCalculator.Generate(
                person,
                [],
                [rate],
                new DateOnly(2026, 8, 8),
                UtcNow));

        Assert.Equal(new DateOnly(2026, 7, 25), charge.DueDate);
        Assert.Equal(857, charge.Amount.MinorUnits);
    }
}
