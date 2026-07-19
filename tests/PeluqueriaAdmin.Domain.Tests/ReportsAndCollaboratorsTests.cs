using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class ReportsAndCollaboratorsTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void MonthlySummary_CalculatesPositiveResultAndOptionalReserveWithoutDuplication()
    {
        var input = new MonthlySummaryInput(
            LocalUseIncomeMinorUnits: 50_000,
            GrossSalesMinorUnits: 30_000,
            OtherIncomeMinorUnits: 20_000,
            ObligationGoalMinorUnits: 20_000,
            MerchandisePurchasesMinorUnits: 10_000,
            MandatoryExpensesMinorUnits: 5_000,
            OptionalSuppliesActualMinorUnits: 3_000,
            OptionalSuppliesBudgetMinorUnits: 4_000,
            UnexpectedExpensesMinorUnits: 1_000,
            MaintenanceGoalMinorUnits: 5_000,
            PendingApprovedPlansMinorUnits: 5_000);

        MonthlySummaryResult result = MonthlySummaryCalculator.Calculate(
            input, Percentage.FromPercent(20m));

        Assert.Equal(100_000, result.IncomeMinorUnits);
        Assert.Equal(50_000, result.GoalMinorUnits);
        Assert.Equal(0, result.MissingMinorUnits);
        Assert.Equal(50_000, result.BaseResultMinorUnits);
        Assert.Equal(10_000, result.CollaboratorFundMinorUnits);
        Assert.Equal(40_000, result.RetainedResultMinorUnits);
    }

    [Fact]
    public void MonthlySummary_NegativeAndZeroResultsNeverCreateCollaboratorDebt()
    {
        var negativeInput = new MonthlySummaryInput(
            1_000, 0, 0, 2_000, 0, 0, 0, 0, 0, 0, 0);
        var zeroInput = new MonthlySummaryInput(
            2_000, 0, 0, 2_000, 0, 0, 0, 0, 0, 0, 0);

        MonthlySummaryResult negative = MonthlySummaryCalculator.Calculate(
            negativeInput, Percentage.FromPercent(20m));
        MonthlySummaryResult zero = MonthlySummaryCalculator.Calculate(
            zeroInput, Percentage.FromPercent(20m));

        Assert.Equal(1_000, negative.MissingMinorUnits);
        Assert.Equal(-1_000, negative.BaseResultMinorUnits);
        Assert.Equal(0, negative.CollaboratorFundMinorUnits);
        Assert.Equal(0, zero.CollaboratorFundMinorUnits);
        Assert.Equal(0, zero.RetainedResultMinorUnits);
    }

    [Fact]
    public void Distribution_AssignsRemainderDeterministicallyAndExactly()
    {
        MonthlySummaryResult summary = new(10_000, 4_995, 0, 5_005, 1_001, 4_004);
        MonthlyClose close = MonthlyClose.Create(
            new YearMonth(2026, 7), Percentage.FromPercent(20m), summary, UtcNow);
        Guid[] ids = [Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002")];

        IReadOnlyList<MonthlyCloseParticipant> participants =
            CollaboratorDistributionCalculator.Distribute(close, ids, UtcNow);

        Assert.Equal(1_001, participants.Sum(item => item.Amount.MinorUnits));
        Assert.Equal([334L, 334L, 333L], participants.Select(item => item.Amount.MinorUnits));
        Assert.Equal(ids.OrderBy(id => id), participants.Select(item => item.CollaboratorId));
    }

    [Fact]
    public void MonthlyClose_KeepsSnapshotAndReopenIsTraceable()
    {
        MonthlySummaryResult summary = new(20_000, 10_000, 0, 10_000, 2_000, 8_000);
        MonthlyClose close = MonthlyClose.Create(
            new YearMonth(2026, 7), Percentage.FromPercent(20m), summary, UtcNow);

        close.Reopen(UtcNow.AddHours(1));

        Assert.Equal(2_000, close.FundMinorUnits);
        Assert.Equal(2_000, close.CollaboratorPercentageBasisPoints);
        Assert.False(close.IsConfirmed);
        Assert.Equal(UtcNow.AddHours(1), close.ReopenedUtc);
        Assert.Throws<InvalidOperationException>(() => close.Reopen(UtcNow.AddHours(2)));
    }

    [Fact]
    public void CashFlowAndAnnualBalance_UseRealMovementsOnce()
    {
        CashMovement[] movements =
        [
            new(new DateOnly(2026, 1, 2), "Ventas", "Venta", 10_000),
            new(new DateOnly(2026, 1, 3), "Compras", "Compra", -4_000),
            new(new DateOnly(2026, 2, 1), "Otros ingresos", "Ingreso", 2_000),
        ];
        MonthlySummaryResult[] months =
        [
            new(10_000, 4_000, 0, 6_000, 1_200, 4_800),
            new(2_000, 3_000, 1_000, -1_000, 0, -1_000),
        ];

        Assert.Equal(6_000, CashFlowCalculator.Balance(
            movements, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));
        AnnualBalanceResult annual = AnnualBalanceCalculator.Calculate(months, 1_200, 500);
        Assert.Equal(12_000, annual.IncomeMinorUnits);
        Assert.Equal(7_000, annual.ExpenseMinorUnits);
        Assert.Equal(3_800, annual.RetainedMinorUnits);
        Assert.Equal(0, annual.MissingMinorUnits);
    }

    [Fact]
    public void FinancialEntries_AreEditableAndSoftDeleted()
    {
        FinancialEntry expense = FinancialEntry.CreateExpense(
            new DateOnly(2026, 7, 1), "Aseo", ExpenseCategory.MandatorySupply,
            Money.FromDecimal(10m), UtcNow);
        expense.Update(
            new DateOnly(2026, 7, 2), "Aseo mensual", ExpenseCategory.MandatorySupply,
            Money.FromDecimal(12m), UtcNow.AddMinutes(1));
        expense.MarkDeleted(UtcNow.AddMinutes(2));

        Assert.Equal("Aseo mensual", expense.Concept);
        Assert.Equal(1_200, expense.Amount.MinorUnits);
        Assert.True(expense.IsDeleted);
        Assert.Equal(UtcNow.AddMinutes(2), expense.DeletedUtc);
    }
}
