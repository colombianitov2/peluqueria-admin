using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class LocalUseTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Generate_CreatesOneChargeForEachElapsedSaturday()
    {
        DateOnly entry = new(2026, 1, 3);
        LocalUsePerson person = LocalUsePerson.Create("Ana", entry, null, UtcNow);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);

        IReadOnlyList<WeeklyCharge> charges = WeeklyChargeCalculator.Generate(
            person, [], [rate], entry.AddDays(21), UtcNow);

        Assert.Collection(
            charges,
            charge => Assert.Equal(new DateOnly(2026, 1, 4), charge.PeriodStart),
            charge => Assert.Equal(new DateOnly(2026, 1, 11), charge.PeriodStart),
            charge => Assert.Equal(new DateOnly(2026, 1, 18), charge.PeriodStart));
        Assert.All(charges, charge => Assert.Equal(charge.PeriodStart.AddDays(6), charge.PeriodEnd));
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
        Assert.Equal(5, historicalCharges.Count);
        Assert.Equal(0, WeeklyChargeCalculator.CalculateDebt(currentCharges, [], today).MinorUnits);
        Assert.Equal(6_000, WeeklyChargeCalculator.CalculateDebt(historicalCharges, [], today).MinorUnits);
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
        Assert.Equal(new DateOnly(2026, 1, 4), generated[0].PeriodStart);
        Assert.Equal(1_200, generated[0].Amount.MinorUnits);
        Assert.Equal(new DateOnly(2026, 1, 11), generated[1].PeriodStart);
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
        Assert.Equal(new DateOnly(2025, 12, 28), charges[0].PeriodStart);
        Assert.Equal(new DateOnly(2026, 1, 3), charges[0].PeriodEnd);
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
    [InlineData(6, 600, 0, 0, 600, 600)]
    [InlineData(12, 1200, 1, 1, 1200, 0)]
    [InlineData(24, 2400, 2, 2, 1200, 0)]
    [InlineData(1000, 100000, 83, 83, 800, 400)]
    public void Account_ProjectsAdvancePaymentsAcrossSaturdayCharges(
        decimal paymentAmount,
        long expectedCredit,
        int expectedCoveredWeeks,
        int expectedRequiredWeeks,
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
        DateOnly firstSaturday = new(2026, 7, 25);
        Assert.Equal(firstSaturday, balance.NextChargeDate);
        Assert.Equal(firstSaturday.AddDays(expectedRequiredWeeks * 7), balance.NextRequiredPaymentDate);
        Assert.Equal(expectedMissing, balance.NextRequiredPaymentAmount?.MinorUnits);
        Assert.Equal(
            expectedCoveredWeeks == 0 ? null : firstSaturday.AddDays((expectedCoveredWeeks - 1) * 7),
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
        Assert.Equal(new DateOnly(2026, 7, 25), firstBalance.CoveredThroughDate);
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
    public void EntryOnJuly19_ChargesOnTheNextSaturdayAndAgainOneWeekLater()
    {
        DateOnly entry = new(2026, 7, 19);
        LocalUsePerson person = LocalUsePerson.Create("Juan", entry, null, UtcNow);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);

        Assert.Empty(WeeklyChargeCalculator.Generate(person, [], [rate], entry, UtcNow));
        IReadOnlyList<WeeklyCharge> onJuly25 = WeeklyChargeCalculator.Generate(
            person, [], [rate], new DateOnly(2026, 7, 25), UtcNow);
        Assert.Single(onJuly25);
        Assert.Equal(new DateOnly(2026, 7, 25), onJuly25[0].PeriodEnd);
        Assert.Equal(new DateOnly(2026, 7, 25), onJuly25[0].DueDate);
        Assert.Equal(1_200, WeeklyChargeCalculator.CalculateDebt(onJuly25, [], new DateOnly(2026, 7, 25)).MinorUnits);

        IReadOnlyList<WeeklyCharge> onAugust1 = WeeklyChargeCalculator.Generate(
            person, onJuly25, [rate], new DateOnly(2026, 8, 1), UtcNow);
        Assert.Single(onAugust1);
        Assert.Equal(new DateOnly(2026, 8, 1), onAugust1[0].DueDate);
    }

    [Fact]
    public void SaturdayBoundaries_AreExactAndRemainIdempotentAfterReopen()
    {
        DateOnly entry = new(2026, 7, 19);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);
        LocalUsePerson active = LocalUsePerson.Create("Lina", entry, null, UtcNow);

        Assert.Empty(WeeklyChargeCalculator.Generate(active, [], [rate], entry, UtcNow));
        Assert.Empty(WeeklyChargeCalculator.Generate(active, [], [rate], new DateOnly(2026, 7, 24), UtcNow));

        IReadOnlyList<WeeklyCharge> firstSaturday = WeeklyChargeCalculator.Generate(
            active, [], [rate], new DateOnly(2026, 7, 25), UtcNow);
        Assert.Single(firstSaturday);
        Assert.Equal(new DateOnly(2026, 7, 25), firstSaturday[0].DueDate);

        IReadOnlyList<WeeklyCharge> secondSaturday = WeeklyChargeCalculator.Generate(
            active, firstSaturday, [rate], new DateOnly(2026, 8, 1), UtcNow.AddMinutes(1));
        Assert.Single(secondSaturday);
        Assert.Equal(new DateOnly(2026, 8, 1), secondSaturday[0].DueDate);

        Assert.Empty(WeeklyChargeCalculator.Generate(
            active, firstSaturday.Concat(secondSaturday), [rate], new DateOnly(2026, 8, 1), UtcNow.AddMinutes(2)));

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
