using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class LocalUseTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Generate_CreatesOnlyCompletedSevenDayPeriods()
    {
        DateOnly entry = new(2026, 1, 3);
        LocalUsePerson person = LocalUsePerson.Create("Ana", entry, null, UtcNow);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);

        IReadOnlyList<WeeklyCharge> charges = WeeklyChargeCalculator.Generate(
            person, [], [rate], entry.AddDays(21), UtcNow);

        Assert.Collection(
            charges,
            charge => Assert.Equal(entry, charge.PeriodStart),
            charge => Assert.Equal(entry.AddDays(7), charge.PeriodStart),
            charge => Assert.Equal(entry.AddDays(14), charge.PeriodStart));
        Assert.All(charges, charge => Assert.Equal(charge.PeriodStart.AddDays(7), charge.PeriodEnd));
        Assert.All(charges, charge => Assert.Equal(charge.PeriodEnd, charge.DueDate));
    }

    [Fact]
    public void EntryOnJuly20OwesZeroWhileEntryOnJune16OwesFortyEightOnJuly20()
    {
        DateOnly today = new(2026, 7, 20);
        WeeklyRate rate = WeeklyRate.Create(new DateOnly(2026, 6, 16), Money.FromDecimal(12m), UtcNow);
        LocalUsePerson current = LocalUsePerson.Create("Actual", today, null, UtcNow);
        LocalUsePerson historical = LocalUsePerson.Create(
            "Histórico", new DateOnly(2026, 6, 16), null, UtcNow);

        IReadOnlyList<WeeklyCharge> currentCharges = WeeklyChargeCalculator.Generate(
            current, [], [rate], today, UtcNow);
        IReadOnlyList<WeeklyCharge> historicalCharges = WeeklyChargeCalculator.Generate(
            historical, [], [rate], today, UtcNow);

        Assert.Empty(currentCharges);
        Assert.Equal(4, historicalCharges.Count);
        Assert.Equal(0, WeeklyChargeCalculator.CalculateDebt(currentCharges, [], today).MinorUnits);
        Assert.Equal(4_800, WeeklyChargeCalculator.CalculateDebt(historicalCharges, [], today).MinorUnits);
    }

    [Fact]
    public void Generate_UsesHistoricalRateAndDoesNotDuplicateExistingPeriods()
    {
        DateOnly entry = new(2026, 1, 1);
        LocalUsePerson person = LocalUsePerson.Create("Luis", entry, null, UtcNow);
        WeeklyRate original = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);
        IReadOnlyList<WeeklyCharge> existing = WeeklyChargeCalculator.Generate(
            person, [], [original], entry.AddDays(7), UtcNow);
        WeeklyRate changed = WeeklyRate.Create(entry.AddDays(14), Money.FromDecimal(15m), UtcNow.AddDays(1));

        IReadOnlyList<WeeklyCharge> generated = WeeklyChargeCalculator.Generate(
            person, existing, [original, changed], entry.AddDays(21), UtcNow.AddDays(1));

        Assert.Equal(2, generated.Count);
        Assert.Equal(entry.AddDays(7), generated[0].PeriodStart);
        Assert.Equal(1_200, generated[0].Amount.MinorUnits);
        Assert.Equal(entry.AddDays(14), generated[1].PeriodStart);
        Assert.Equal(1_500, generated[1].Amount.MinorUnits);
        Assert.All(existing, charge => Assert.Equal(1_200, charge.Amount.MinorUnits));
        Assert.Empty(WeeklyChargeCalculator.Generate(
            person, existing.Concat(generated), [original, changed], entry.AddDays(21), UtcNow.AddDays(2)));
    }

    [Fact]
    public void Generate_StopsAfterExitButKeepsStartedPeriodComplete()
    {
        DateOnly entry = new(2026, 1, 1);
        LocalUsePerson person = LocalUsePerson.Create("Marta", entry, entry.AddDays(9), UtcNow);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);

        IReadOnlyList<WeeklyCharge> charges = WeeklyChargeCalculator.Generate(
            person, [], [rate], entry.AddDays(30), UtcNow);

        Assert.Single(charges);
        Assert.Equal(entry, charges[0].PeriodStart);
        Assert.Equal(entry.AddDays(7), charges[0].PeriodEnd);
    }

    [Fact]
    public void Payment_AllowsPartialAndAdvanceAmountsWithoutNegativeDebt()
    {
        DateOnly entry = new(2026, 1, 1);
        LocalUsePerson person = LocalUsePerson.Create("Sara", entry, null, UtcNow);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);
        IReadOnlyList<WeeklyCharge> charges = WeeklyChargeCalculator.Generate(
            person, [], [rate], entry.AddDays(7), UtcNow);
        Money initialDebt = WeeklyChargeCalculator.CalculateDebt(charges, []);

        LocalUsePayment partial = LocalUsePayment.Create(
            person.Id, entry.AddDays(8), Money.FromDecimal(5m), UtcNow);
        LocalUsePayment advance = LocalUsePayment.Create(
            person.Id, entry.AddDays(9), Money.FromDecimal(20m), UtcNow);
        Money remaining = WeeklyChargeCalculator.CalculateDebt(charges, [partial]);
        Money afterAdvance = WeeklyChargeCalculator.CalculateDebt(charges, [partial, advance]);

        Assert.Equal(1_200, initialDebt.MinorUnits);
        Assert.Equal(700, remaining.MinorUnits);
        Assert.Equal(0, afterAdvance.MinorUnits);
    }

    [Theory]
    [InlineData(6, 600, 0, 13, 600, 600)]
    [InlineData(12, 1200, 7, 20, 1200, 0)]
    [InlineData(24, 2400, 14, 27, 1200, 0)]
    [InlineData(1000, 100000, 581, 594, 800, 400)]
    public void Account_ProjectsAdvancePaymentsAcrossAnchoredSevenDayCycles(
        decimal paymentAmount,
        long expectedCredit,
        int expectedCoveredDays,
        int expectedRequiredDays,
        long expectedMissing,
        long expectedRemainder)
    {
        DateOnly entry = new(2026, 7, 19);
        LocalUsePerson person = LocalUsePerson.Create("Sara", entry, null, UtcNow);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);
        LocalUsePayment payment = LocalUsePayment.Create(
            person.Id, entry, Money.FromDecimal(paymentAmount), UtcNow);

        WorkerAccountBalance balance = WeeklyChargeCalculator.CalculateAccount(
            person, [], [payment], [rate], entry);

        Assert.Equal(0, balance.Debt.MinorUnits);
        Assert.Equal(expectedCredit, balance.Credit.MinorUnits);
        Assert.Equal(entry.AddDays(7), balance.NextChargeDate);
        Assert.Equal(entry.AddDays(expectedRequiredDays), balance.NextRequiredPaymentDate);
        Assert.Equal(expectedMissing, balance.NextRequiredPaymentAmount?.MinorUnits);
        Assert.Equal(
            expectedCoveredDays == 0 ? null : entry.AddDays(expectedCoveredDays),
            balance.CoveredThroughDate);
        Assert.Equal(expectedRemainder, expectedCredit % 1_200);
    }

    [Fact]
    public void Account_WorkerOwingTwentyFourWhoPaysOneThousandKeepsNineHundredSeventySixCredit()
    {
        DateOnly entry = new(2026, 1, 1);
        DateOnly today = entry.AddDays(14);
        LocalUsePerson person = LocalUsePerson.Create("Luis", entry, null, UtcNow);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);
        IReadOnlyList<WeeklyCharge> charges = WeeklyChargeCalculator.Generate(person, [], [rate], today, UtcNow);
        LocalUsePayment payment = LocalUsePayment.Create(
            person.Id, today, Money.FromDecimal(1000m), UtcNow);

        WorkerAccountBalance balance = WeeklyChargeCalculator.CalculateAccount(
            person, charges, [payment], [rate], today);

        Assert.Equal(2_400, balance.TotalCharged.MinorUnits);
        Assert.Equal(100_000, balance.TotalPaid.MinorUnits);
        Assert.Equal(0, balance.Debt.MinorUnits);
        Assert.Equal(97_600, balance.Credit.MinorUnits);
    }

    [Fact]
    public void Account_AppliesMultiplePartialPaymentsAndFutureRateIndependentlyPerWorker()
    {
        DateOnly entry = new(2026, 7, 19);
        LocalUsePerson first = LocalUsePerson.Create("Ana", entry, null, UtcNow);
        LocalUsePerson second = LocalUsePerson.Create("Beto", entry, null, UtcNow);
        WeeklyRate original = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);
        WeeklyRate changed = WeeklyRate.Create(entry.AddDays(7), Money.FromDecimal(20m), UtcNow.AddHours(1));
        LocalUsePayment firstPart = LocalUsePayment.Create(first.Id, entry, Money.FromDecimal(5m), UtcNow);
        LocalUsePayment secondPart = LocalUsePayment.Create(first.Id, entry, Money.FromDecimal(19m), UtcNow.AddMinutes(1));
        LocalUsePayment otherWorker = LocalUsePayment.Create(second.Id, entry, Money.FromDecimal(100m), UtcNow);

        WorkerAccountBalance firstBalance = WeeklyChargeCalculator.CalculateAccount(
            first, [], [firstPart, secondPart, otherWorker], [original, changed], entry);
        WorkerAccountBalance secondBalance = WeeklyChargeCalculator.CalculateAccount(
            second, [], [firstPart, secondPart, otherWorker], [original, changed], entry);

        Assert.Equal(2_400, firstBalance.Credit.MinorUnits);
        Assert.Equal(entry.AddDays(7), firstBalance.CoveredThroughDate);
        Assert.Equal(800, firstBalance.NextRequiredPaymentAmount?.MinorUnits);
        Assert.Equal(10_000, secondBalance.Credit.MinorUnits);
    }

    [Fact]
    public void Account_RetirementStopsFutureChargesAndPreservesCredit()
    {
        DateOnly entry = new(2026, 7, 1);
        DateOnly exit = entry.AddDays(7);
        LocalUsePerson person = LocalUsePerson.Create("Nora", entry, exit, UtcNow);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);
        IReadOnlyList<WeeklyCharge> charges = WeeklyChargeCalculator.Generate(person, [], [rate], exit, UtcNow);
        LocalUsePayment payment = LocalUsePayment.Create(
            person.Id, exit, Money.FromDecimal(24m), UtcNow);

        WorkerAccountBalance balance = WeeklyChargeCalculator.CalculateAccount(
            person, charges, [payment], [rate], exit);

        Assert.Equal(1_200, balance.Credit.MinorUnits);
        Assert.Null(balance.NextChargeDate);
        Assert.Null(balance.NextRequiredPaymentDate);
    }

    [Fact]
    public void EntryOnJuly19_DoesNotChargeUntilJuly26_AndStillOwesOnlyTwelveOnAugust1()
    {
        DateOnly entry = new(2026, 7, 19);
        LocalUsePerson person = LocalUsePerson.Create("Juan", entry, null, UtcNow);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);

        Assert.Empty(WeeklyChargeCalculator.Generate(person, [], [rate], entry, UtcNow));
        Assert.Empty(WeeklyChargeCalculator.Generate(person, [], [rate], new DateOnly(2026, 7, 25), UtcNow));

        IReadOnlyList<WeeklyCharge> onJuly26 = WeeklyChargeCalculator.Generate(
            person, [], [rate], new DateOnly(2026, 7, 26), UtcNow);
        Assert.Single(onJuly26);
        Assert.Equal(new DateOnly(2026, 7, 26), onJuly26[0].PeriodEnd);
        Assert.Equal(new DateOnly(2026, 8, 1), onJuly26[0].DueDate);
        Assert.Equal(1_200, WeeklyChargeCalculator.CalculateDebt(onJuly26, [], new DateOnly(2026, 8, 1)).MinorUnits);

        IReadOnlyList<WeeklyCharge> onAugust1 = WeeklyChargeCalculator.Generate(
            person, onJuly26, [rate], new DateOnly(2026, 8, 1), UtcNow);
        Assert.Empty(onAugust1);
    }

    [Fact]
    public void SevenDayBoundaries_AreExactAndRemainIdempotentAfterReopen()
    {
        DateOnly entry = new(2026, 7, 19);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);
        LocalUsePerson active = LocalUsePerson.Create("Lina", entry, null, UtcNow);

        Assert.Empty(WeeklyChargeCalculator.Generate(active, [], [rate], entry, UtcNow));
        Assert.Empty(WeeklyChargeCalculator.Generate(active, [], [rate], entry.AddDays(6), UtcNow));

        IReadOnlyList<WeeklyCharge> day7 = WeeklyChargeCalculator.Generate(
            active, [], [rate], entry.AddDays(7), UtcNow);
        Assert.Single(day7);
        Assert.Equal(entry, day7[0].PeriodStart);

        IReadOnlyList<WeeklyCharge> day14 = WeeklyChargeCalculator.Generate(
            active, day7, [rate], entry.AddDays(14), UtcNow.AddMinutes(1));
        Assert.Single(day14);
        Assert.Equal(entry.AddDays(7), day14[0].PeriodStart);

        Assert.Empty(WeeklyChargeCalculator.Generate(
            active, day7.Concat(day14), [rate], entry.AddDays(14), UtcNow.AddMinutes(2)));

        LocalUsePerson retiredEarly = LocalUsePerson.Create(
            "Nora", entry, entry.AddDays(6), UtcNow);
        Assert.Empty(WeeklyChargeCalculator.Generate(
            retiredEarly, [], [rate], entry.AddDays(30), UtcNow));
    }

    [Fact]
    public void ExitDate_IsExclusiveForCurrentState_WithoutChangingCompletedWeeks()
    {
        DateOnly entry = new(2026, 7, 1);
        DateOnly exit = entry.AddDays(7);
        LocalUsePerson person = LocalUsePerson.Create("Ana", entry, exit, UtcNow);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);

        Assert.True(person.IsCurrentOn(exit.AddDays(-1)));
        Assert.False(person.IsCurrentOn(exit));
        Assert.Single(WeeklyChargeCalculator.Generate(person, [], [rate], exit, UtcNow));
    }
}
