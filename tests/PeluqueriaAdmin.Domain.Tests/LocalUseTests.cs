using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class LocalUseTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Generate_CreatesFirstChargeOnEntryAndThenEverySevenDays()
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
            charge => Assert.Equal(entry.AddDays(14), charge.PeriodStart),
            charge => Assert.Equal(entry.AddDays(21), charge.PeriodStart));
        Assert.All(charges, charge => Assert.Equal(charge.PeriodStart.AddDays(6), charge.PeriodEnd));
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
        Assert.Equal(entry.AddDays(14), generated[0].PeriodStart);
        Assert.Equal(1_500, generated[0].Amount.MinorUnits);
        Assert.Equal(entry.AddDays(21), generated[1].PeriodStart);
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

        Assert.Equal(2, charges.Count);
        Assert.Equal(entry.AddDays(7), charges[1].PeriodStart);
        Assert.Equal(entry.AddDays(13), charges[1].PeriodEnd);
    }

    [Fact]
    public void Payment_AllowsPartialAmountAndRejectsOverpayment()
    {
        DateOnly entry = new(2026, 1, 1);
        LocalUsePerson person = LocalUsePerson.Create("Sara", entry, null, UtcNow);
        WeeklyRate rate = WeeklyRate.Create(entry, Money.FromDecimal(12m), UtcNow);
        IReadOnlyList<WeeklyCharge> charges = WeeklyChargeCalculator.Generate(
            person, [], [rate], entry.AddDays(7), UtcNow);
        Money initialDebt = WeeklyChargeCalculator.CalculateDebt(charges, []);

        LocalUsePayment partial = LocalUsePayment.Create(
            person.Id, entry.AddDays(8), Money.FromDecimal(5m), initialDebt, UtcNow);
        Money remaining = WeeklyChargeCalculator.CalculateDebt(charges, [partial]);

        Assert.Equal(2_400, initialDebt.MinorUnits);
        Assert.Equal(1_900, remaining.MinorUnits);
        Assert.Throws<InvalidOperationException>(() => LocalUsePayment.Create(
            person.Id, entry.AddDays(9), Money.FromDecimal(20m), remaining, UtcNow));
    }
}
