using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.Activity;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Tests;

public sealed class Phase49ApplicationTests
{
    private static readonly DateTime Utc = new(2026, 7, 23, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Phase49_01_AssignmentAndEditRemainSeparateExactOperations()
    {
        ActivityRecord assignment = Activity("Asignación", Utc);
        ActivityRecord edit = Activity("Edición", Utc.AddMinutes(1));
        Assert.Equal(["Asignación", "Edición"], new[] { assignment.Action, edit.Action });
        Assert.DoesNotContain("Asignación o edición", new[] { assignment.Action, edit.Action });
    }

    [Fact]
    public void Phase49_02_DailyQueryReturnsOnlyTheSelectedLocalDateDescending()
    {
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("Bogotá prueba", TimeSpan.FromHours(-5), "Prueba", "Prueba");
        ActivityRecord yesterday = Activity("Creación", new DateTime(2026, 7, 22, 15, 0, 0, DateTimeKind.Utc));
        ActivityRecord todayEarly = Activity("Pago", new DateTime(2026, 7, 23, 6, 0, 0, DateTimeKind.Utc));
        ActivityRecord todayLate = Activity("Venta", new DateTime(2026, 7, 24, 4, 59, 0, DateTimeKind.Utc));
        ActivityRecord tomorrow = Activity("Compra", new DateTime(2026, 7, 24, 5, 0, 0, DateTimeKind.Utc));

        IReadOnlyList<ActivityRecord> result = DailyActivityQuery.ForLocalDate(
            [yesterday, todayEarly, todayLate, tomorrow], new DateOnly(2026, 7, 23), zone);

        Assert.Equal(["Venta", "Pago"], result.Select(item => item.Action));
    }

    [Fact]
    public void Phase49_03_UtcMidnightBoundaryUsesTheUsersLocalDate()
    {
        TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("Bogotá prueba", TimeSpan.FromHours(-5), "Prueba", "Prueba");
        ActivityRecord nearUtcMidnight = Activity("Pago", new DateTime(2026, 7, 24, 2, 0, 0, DateTimeKind.Utc));
        Assert.Single(DailyActivityQuery.ForLocalDate([nearUtcMidnight], new DateOnly(2026, 7, 23), zone));
        Assert.Empty(DailyActivityQuery.ForLocalDate([nearUtcMidnight], new DateOnly(2026, 7, 24), zone));
    }

    [Fact]
    public void Phase49_26_OverdueLoanInstallmentAppearsOnHomeWithCalculatedAmount()
    {
        LoanPlan plan = LoanCalculator.AgreedFinalAmount("Préstamo 100 a 150", Money.FromDecimal(100m),
            Money.FromDecimal(150m), 5, new DateOnly(2026, 7, 1), Utc, "Caso de revisión");
        HomeDashboard result = HomeDashboardCalculator.Calculate(
            EmptyData() with { Loans = [plan.Loan], LoanInstallments = plan.Installments },
            Percentage.FromPercent(20m), new DateOnly(2026, 7, 23));
        PendingHomeObligation installment = Assert.Single(result.Obligations);
        Assert.Equal("Cuota de préstamo", installment.Type);
        Assert.Equal(3_000, installment.Amount.MinorUnits);
        Assert.Equal("Vencido", installment.Status);
    }

    [Fact]
    public void Phase49_33_OpenMonthUsesLiveMovements()
    {
        FinancialEntry income = FinancialEntry.CreateIncome(
            new DateOnly(2026, 7, 20), "Ingreso vivo", Money.FromDecimal(125m), Utc);
        AnnualFinancialReport report = AnnualFinancialCalculator.Calculate(
            EmptyData() with { FinancialEntries = [income] }, Percentage.FromPercent(0m), 2026,
            new DateOnly(2026, 7, 23));
        Assert.Equal(12_500, report.Months[6].IncomeMinorUnits);
        Assert.False(report.Months[6].IsClosed);
    }

    [Fact]
    public void Phase49_34_ClosedMonthUsesSnapshotWithoutDoubleCountingLiveEntries()
    {
        FinancialEntry live = FinancialEntry.CreateIncome(
            new DateOnly(2026, 7, 20), "Ingreso posterior", Money.FromDecimal(999m), Utc);
        MonthlyClose close = MonthlyClose.Create(Snapshot(new YearMonth(2026, 7), 10_000, 2_000), Utc);
        AnnualFinancialReport report = AnnualFinancialCalculator.Calculate(
            EmptyData() with { FinancialEntries = [live], MonthlyCloses = [close] },
            Percentage.FromPercent(0m), 2026, new DateOnly(2026, 7, 23));
        Assert.Equal(10_000, report.Months[6].IncomeMinorUnits);
        Assert.True(report.Months[6].IsClosed);
    }

    [Fact]
    public void Phase49_35_ClosedAnnualSnapshotIsImmutableForHistoricalQueries()
    {
        AnnualClose close = AnnualClose.Create(2025, 50_000, 20_000, 3_000, 4_000, 5_000,
            6_000, 30_000, 7_000, 8_000, 9_000, 10_000, 30_000, 0, 12_000, 20_000, Utc);
        FinancialEntry later = FinancialEntry.CreateIncome(
            new DateOnly(2025, 12, 31), "No reabrir snapshot", Money.FromDecimal(999m), Utc);
        AnnualFinancialReport report = AnnualFinancialCalculator.Calculate(
            EmptyData() with { AnnualCloses = [close], FinancialEntries = [later] },
            Percentage.FromPercent(0m), 2025, new DateOnly(2026, 7, 23));
        Assert.True(report.IsClosed);
        Assert.Equal(50_000, report.IncomeMinorUnits);
        Assert.Equal(20_000, report.ProjectedNextYearBalanceMinorUnits);
    }

    [Fact]
    public void Phase49_36_AnnualCarryoverKeepsPendingAccountsSeparated()
    {
        AnnualCarryover carryover = AnnualCarryover.Create(2026, 1_000, 2_000, 3_000, 4_000, 0, 0, Utc);
        Assert.Equal(2027, carryover.TargetYear);
        Assert.Equal(1_000, carryover.AccountsReceivableMinorUnits);
        Assert.Equal(2_000, carryover.AccountsPayableMinorUnits);
        Assert.Equal(3_000, carryover.PendingReservesMinorUnits);
        Assert.Equal(4_000, carryover.PendingLoansMinorUnits);
    }

    [Fact]
    public void Phase49_37_AnnualCarryoverKeepsSurplusAndDeficitSeparate()
    {
        AnnualCarryover surplus = AnnualCarryover.Create(2026, 0, 0, 0, 0, 5_000, 0, Utc);
        AnnualCarryover deficit = AnnualCarryover.Create(2027, 0, 0, 0, 0, 0, 3_000, Utc);
        Assert.Equal(5_000, surplus.SurplusMinorUnits);
        Assert.Equal(0, surplus.DeficitMinorUnits);
        Assert.Equal(0, deficit.SurplusMinorUnits);
        Assert.Equal(3_000, deficit.DeficitMinorUnits);
    }

    private static ActivityRecord Activity(string action, DateTime occurredUtc) =>
        ActivityRecord.Create(DateOnly.FromDateTime(occurredUtc), "Prueba", action,
            $"Operación {action}", Guid.NewGuid(), null, occurredUtc);

    private static FinancialMonthSnapshot Snapshot(YearMonth month, long income, long result) => new(
        month, income, 0, 0, 0, 0, 0, 0, 0, 0, 0, result, 0,
        Math.Max(0, -result), 0, Math.Max(0, result), 0, []);

    private static AdministrationData EmptyData() => new(
        [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], []);
}
