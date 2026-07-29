using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class DailyChargeTests
{
    private static readonly DateTime UtcNow =
        new(2026, 7, 27, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(27, 6, 300)]
    [InlineData(29, 4, 200)]
    [InlineData(1, 1, 50)]
    public void DailyFee_ChargesExactMondayToSaturdayAmounts(
        int entryDay,
        int expectedDays,
        decimal expectedAmount)
    {
        DateOnly entry = entryDay == 1
            ? new DateOnly(2026, 8, 1)
            : new DateOnly(2026, 7, entryDay);
        DateOnly saturday = new(2026, 8, 1);
        LocalUsePerson person = LocalUsePerson.Create("Trabajador", entry, null, UtcNow);
        Chair chair = Chair.Create("Silla 1", entry, null, UtcNow);
        ChairAssignmentPeriod assignment =
            ChairAssignmentPeriod.Create(chair.Id, person.Id, entry, UtcNow);
        DailyRate rate = DailyRate.Create(
            new DateOnly(2026, 7, 27),
            UtcNow,
            Money.FromDecimal(50m),
            UtcNow);

        IReadOnlyList<DailyCharge> charges = DailyChargeCalculator.Generate(
            person, [], [rate], [assignment], saturday, UtcNow);

        Assert.Equal(expectedDays, charges.Count);
        Assert.Equal(expectedAmount, charges.Sum(item => item.Amount.ToDecimal()));
        Assert.All(charges, item => Assert.Equal(saturday, item.DueDate));
    }

    [Fact]
    public void Sunday_GeneratesNoCharge_AndFirstChargeIsMonday()
    {
        DateOnly sunday = new(2026, 8, 2);
        LocalUsePerson person = LocalUsePerson.Create("Domingo", sunday, null, UtcNow);
        Chair chair = Chair.Create("Silla", sunday, null, UtcNow);
        ChairAssignmentPeriod assignment =
            ChairAssignmentPeriod.Create(chair.Id, person.Id, sunday, UtcNow);
        DailyRate rate = DailyRate.Create(sunday, UtcNow, Money.FromDecimal(50m), UtcNow);

        Assert.Empty(DailyChargeCalculator.Generate(
            person, [], [rate], [assignment], sunday, UtcNow));

        DailyCharge monday = Assert.Single(DailyChargeCalculator.Generate(
            person, [], [rate], [assignment], sunday.AddDays(1), UtcNow));
        Assert.Equal(DayOfWeek.Monday, monday.ChargeDate.DayOfWeek);
        Assert.Equal(50m, monday.Amount.ToDecimal());
    }

    [Fact]
    public void ZeroRate_GeneratesExactZeroDailyRecordsWithoutDebt()
    {
        DateOnly monday = new(2026, 7, 27);
        DateOnly saturday = new(2026, 8, 1);
        LocalUsePerson person = LocalUsePerson.Create("Cero", monday, null, UtcNow);
        Chair chair = Chair.Create("Silla", monday, null, UtcNow);
        ChairAssignmentPeriod assignment =
            ChairAssignmentPeriod.Create(chair.Id, person.Id, monday, UtcNow);
        DailyRate rate = DailyRate.Create(monday, UtcNow, Money.FromDecimal(0m), UtcNow);

        IReadOnlyList<DailyCharge> charges = DailyChargeCalculator.Generate(
            person, [], [rate], [assignment], saturday, UtcNow);

        Assert.Equal(6, charges.Count);
        Assert.All(charges, charge => Assert.Equal(0, charge.Amount.MinorUnits));
        Assert.Equal(0, DailyChargeCalculator.CalculateDebt(
            charges, [], [], saturday).MinorUnits);
    }

    [Fact]
    public void MidweekRateChange_PreservesEarlierDailyAmounts()
    {
        DateOnly monday = new(2026, 7, 27);
        DateOnly wednesday = new(2026, 7, 29);
        DateOnly saturday = new(2026, 8, 1);
        LocalUsePerson person = LocalUsePerson.Create("Histórico", monday, null, UtcNow);
        Chair chair = Chair.Create("Silla", monday, null, UtcNow);
        ChairAssignmentPeriod assignment =
            ChairAssignmentPeriod.Create(chair.Id, person.Id, monday, UtcNow);
        DailyRate ten = DailyRate.Create(monday, UtcNow, Money.FromDecimal(10m), UtcNow);
        DailyRate fifteen = DailyRate.Create(
            wednesday,
            UtcNow.AddDays(2),
            Money.FromDecimal(15m),
            UtcNow.AddDays(2));

        IReadOnlyList<DailyCharge> charges = DailyChargeCalculator.Generate(
            person, [], [ten, fifteen], [assignment], saturday, UtcNow.AddDays(5));

        Assert.Equal([10m, 10m, 15m, 15m, 15m, 15m],
            charges.Select(item => item.Amount.ToDecimal()).ToArray());
        Assert.Equal(80m, charges.Sum(item => item.Amount.ToDecimal()));
    }

    [Fact]
    public void UnconfiguredRateGap_GeneratesNoChargesAndNeverFallsBack()
    {
        DateOnly monday = new(2026, 7, 27);
        DateOnly wednesday = monday.AddDays(2);
        DateOnly friday = monday.AddDays(4);
        DateOnly saturday = monday.AddDays(5);
        LocalUsePerson person = LocalUsePerson.Create("Con intervalo vacío", monday, null, UtcNow);
        Chair chair = Chair.Create("Silla", monday, null, UtcNow);
        ChairAssignmentPeriod assignment =
            ChairAssignmentPeriod.Create(chair.Id, person.Id, monday, UtcNow);
        DailyRate ten = DailyRate.Create(monday, UtcNow, Money.FromDecimal(10m), UtcNow);
        ten.Close(wednesday, UtcNow.AddDays(2));
        DailyRate fifteen = DailyRate.Create(
            friday,
            UtcNow.AddDays(4),
            Money.FromDecimal(15m),
            UtcNow.AddDays(4));

        IReadOnlyList<DailyCharge> charges = DailyChargeCalculator.Generate(
            person, [], [ten, fifteen], [assignment], saturday, UtcNow.AddDays(5));

        Assert.Equal(
            [monday, monday.AddDays(1), friday, saturday],
            charges.Select(item => item.ChargeDate).ToArray());
        Assert.Equal([10m, 10m, 15m, 15m],
            charges.Select(item => item.Amount.ToDecimal()).ToArray());
        Assert.Null(DailyChargeCalculator.TryRateFor([ten, fifteen], wednesday));
        Assert.Throws<InvalidOperationException>(
            () => DailyChargeCalculator.RateFor([ten, fifteen], wednesday));
    }

    [Fact]
    public void MissingOrClosedChairAssignment_StopsFutureCharges()
    {
        DateOnly monday = new(2026, 7, 27);
        LocalUsePerson person = LocalUsePerson.Create("Sin silla", monday, null, UtcNow);
        DailyRate rate = DailyRate.Create(monday, UtcNow, Money.FromDecimal(50m), UtcNow);
        Assert.Empty(DailyChargeCalculator.Generate(
            person, [], [rate], [], monday.AddDays(5), UtcNow));

        Chair chair = Chair.Create("Silla", monday, null, UtcNow);
        ChairAssignmentPeriod assignment =
            ChairAssignmentPeriod.Create(chair.Id, person.Id, monday, UtcNow);
        assignment.Close(monday.AddDays(2), UtcNow.AddDays(2));
        IReadOnlyList<DailyCharge> charges = DailyChargeCalculator.Generate(
            person, [], [rate], [assignment], monday.AddDays(5), UtcNow.AddDays(5));
        Assert.Equal([monday, monday.AddDays(1)],
            charges.Select(item => item.ChargeDate).ToArray());
    }

    [Fact]
    public void DeletedWorker_GeneratesNoNewCharges()
    {
        DateOnly monday = new(2026, 7, 27);
        LocalUsePerson person = LocalUsePerson.Create("Eliminado", monday, null, UtcNow);
        Chair chair = Chair.Create("Silla", monday, null, UtcNow);
        ChairAssignmentPeriod assignment =
            ChairAssignmentPeriod.Create(chair.Id, person.Id, monday, UtcNow);
        DailyRate rate = DailyRate.Create(monday, UtcNow, Money.FromDecimal(50m), UtcNow);
        person.MarkDeleted(UtcNow.AddDays(1));

        Assert.Empty(DailyChargeCalculator.Generate(
            person, [], [rate], [assignment], monday.AddDays(5), UtcNow.AddDays(5)));
    }

    [Fact]
    public void AdvancePayment_CreditIsConsumedByChargeableDaysAndSkipsSunday()
    {
        DateOnly monday = new(2026, 7, 27);
        DateOnly tuesday = monday.AddDays(1);
        LocalUsePerson person = LocalUsePerson.Create("Anticipo", monday, null, UtcNow);
        Chair chair = Chair.Create("Silla", monday, null, UtcNow);
        ChairAssignmentPeriod assignment =
            ChairAssignmentPeriod.Create(chair.Id, person.Id, monday, UtcNow);
        DailyRate rate = DailyRate.Create(monday, UtcNow, Money.FromDecimal(50m), UtcNow);
        IReadOnlyList<DailyCharge> charges = DailyChargeCalculator.Generate(
            person, [], [rate], [assignment], tuesday, UtcNow);
        LocalUsePayment payment = LocalUsePayment.Create(
            person.Id,
            monday,
            Money.FromDecimal(300m),
            UtcNow);

        WorkerAccountBalance account = DailyChargeCalculator.CalculateAccount(
            person, charges, [], [payment], [rate], [assignment], tuesday);

        Assert.Equal(200m, account.Credit.ToDecimal());
        Assert.Equal(new DateOnly(2026, 8, 8), account.NextChargeDate);
        Assert.Equal(new DateOnly(2026, 8, 8), account.NextRequiredPaymentDate);
        Assert.Equal(300m, account.NextRequiredPaymentAmount?.ToDecimal());
        Assert.NotEqual(DayOfWeek.Sunday, account.CoveredThroughDate?.DayOfWeek);
    }

    [Fact]
    public void PartialPayment_CoversOldestChargesAndLeavesExactDebtForSaturday()
    {
        DateOnly monday = new(2026, 7, 27);
        DateOnly saturday = new(2026, 8, 1);
        LocalUsePerson person = LocalUsePerson.Create("Pago parcial", monday, null, UtcNow);
        Chair chair = Chair.Create("Silla", monday, null, UtcNow);
        ChairAssignmentPeriod assignment =
            ChairAssignmentPeriod.Create(chair.Id, person.Id, monday, UtcNow);
        DailyRate rate = DailyRate.Create(monday, UtcNow, Money.FromDecimal(50m), UtcNow);
        IReadOnlyList<DailyCharge> charges = DailyChargeCalculator.Generate(
            person, [], [rate], [assignment], saturday, UtcNow);
        LocalUsePayment payment = LocalUsePayment.Create(
            person.Id, saturday, Money.FromDecimal(125m), UtcNow);

        WorkerAccountBalance account = DailyChargeCalculator.CalculateAccount(
            person, charges, [], [payment], [rate], [assignment], saturday);

        Assert.Equal(175m, account.Debt.ToDecimal());
        Assert.Equal(0m, account.Credit.ToDecimal());
        Assert.Equal(saturday, account.NextRequiredPaymentDate);
        Assert.Equal(175m, account.NextRequiredPaymentAmount?.ToDecimal());
    }

    [Fact]
    public void MonthAndYearBoundary_UsesEachDatesHistoricalRateWithoutRewriting()
    {
        DateOnly monday = new(2025, 12, 29);
        DateOnly newYear = new(2026, 1, 1);
        DateOnly saturday = new(2026, 1, 3);
        DateTime utc = new(2025, 12, 29, 12, 0, 0, DateTimeKind.Utc);
        LocalUsePerson person = LocalUsePerson.Create("Cambio de año", monday, null, utc);
        Chair chair = Chair.Create("Silla", monday, null, utc);
        ChairAssignmentPeriod assignment =
            ChairAssignmentPeriod.Create(chair.Id, person.Id, monday, utc);
        DailyRate ten = DailyRate.Create(monday, utc, Money.FromDecimal(10m), utc);
        DailyRate fifteen = DailyRate.Create(
            newYear, utc.AddDays(3), Money.FromDecimal(15m), utc.AddDays(3));

        IReadOnlyList<DailyCharge> initial = DailyChargeCalculator.Generate(
            person, [], [ten], [assignment], newYear.AddDays(-1), utc.AddDays(2));
        IReadOnlyList<DailyCharge> later = DailyChargeCalculator.Generate(
            person, initial, [ten, fifteen], [assignment], saturday, utc.AddDays(5));

        Assert.Equal([10m, 10m, 10m], initial.Select(item => item.Amount.ToDecimal()).ToArray());
        Assert.Equal([15m, 15m, 15m], later.Select(item => item.Amount.ToDecimal()).ToArray());
        Assert.Equal(75m, initial.Concat(later).Sum(item => item.Amount.ToDecimal()));
        Assert.All(initial.Concat(later), charge => Assert.Equal(saturday, charge.DueDate));
    }
}
