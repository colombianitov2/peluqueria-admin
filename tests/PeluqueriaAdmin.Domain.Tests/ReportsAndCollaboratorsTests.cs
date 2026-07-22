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
    public void MonthlySummary_IgnoresRetiredOptionalBudgetAndUsesOnlyActualCosts()
    {
        var input = new MonthlySummaryInput(
            LocalUseEarnedIncomeMinorUnits: 50_000,
            GrossSalesMinorUnits: 30_000,
            OtherIncomeMinorUnits: 20_000,
            InventoryPurchasesMinorUnits: 10_000,
            RegisteredExpensesMinorUnits: 8_000,
            UnexpectedExpensesMinorUnits: 1_000,
            ObligationPaymentsMinorUnits: 20_000,
            CompletedMaintenanceMinorUnits: 5_000);

        MonthlySummaryResult result = MonthlySummaryCalculator.Calculate(
            input, Percentage.FromPercent(20m));

        Assert.Equal(100_000, result.IncomeMinorUnits);
        Assert.Equal(44_000, result.GoalMinorUnits);
        Assert.Equal(0, result.MissingMinorUnits);
        Assert.Equal(56_000, result.BaseResultMinorUnits);
        Assert.Equal(11_200, result.CollaboratorFundMinorUnits);
        Assert.Equal(44_800, result.RetainedResultMinorUnits);
    }

    [Fact]
    public void MonthlySummary_NegativeAndZeroResultsNeverCreateCollaboratorDebt()
    {
        var negativeInput = new MonthlySummaryInput(
            1_000, 0, 0, 2_000, 0, 0, 0, 0);
        var zeroInput = new MonthlySummaryInput(
            2_000, 0, 0, 2_000, 0, 0, 0, 0);

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
    public void Distribution_UsesIndividualPercentagesInExactMinorUnits()
    {
        MonthlySummaryResult summary = new(10_000, 4_995, 0, 5_005, 1_001, 4_004);
        MonthlyClose close = MonthlyClose.Create(
            new YearMonth(2026, 7), Percentage.FromPercent(20m), summary, UtcNow);
        Guid[] ids = [Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Guid.Parse("00000000-0000-0000-0000-000000000004")];

        IReadOnlyList<MonthlyCloseParticipant> participants =
            CollaboratorDistributionCalculator.Distribute(
                close,
                new[] { (ids[0], 1_200), (ids[1], 400), (ids[2], 200), (ids[3], 200) },
                UtcNow);

        Assert.Equal(200, participants.Sum(item => item.Amount.MinorUnits));
        Assert.Equal([120L, 40L, 20L, 20L], participants.Select(item => item.Amount.MinorUnits));
        Assert.Equal(ids, participants.Select(item => item.CollaboratorId));
    }

    [Fact]
    public void Distribution_ExactApprovedExample_20Global_60_20_10_10Internal()
    {
        MonthlySummaryResult summary = new(100_000, 0, 0, 100_000, 20_000, 80_000);
        MonthlyClose close = MonthlyClose.Create(
            new YearMonth(2026, 7), Percentage.FromPercent(20m), summary, UtcNow);
        Guid[] ids = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()];

        IReadOnlyList<MonthlyCloseParticipant> participants =
            CollaboratorDistributionCalculator.Distribute(
                close,
                new[] { (ids[0], 6_000), (ids[1], 2_000), (ids[2], 1_000), (ids[3], 1_000) },
                UtcNow);

        Assert.Equal([12_000L, 4_000L, 2_000L, 2_000L],
            participants.OrderBy(item => Array.IndexOf(ids, item.CollaboratorId)).Select(item => item.Amount.MinorUnits));
        Assert.Equal(20_000, participants.Sum(item => item.Amount.MinorUnits));
    }

    [Fact]
    public void Distribution_RejectsInternalParticipationAboveOneHundredPercent()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            CollaboratorDistributionCalculator.CalculateMinorUnitAmounts(
                100_000,
                2_000,
                [(Guid.NewGuid(), 6_000), (Guid.NewGuid(), 4_001)]));

        Assert.Contains("100 %", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Distribution_UnassignedFundRemainsOutsideParticipantPayments()
    {
        IReadOnlyDictionary<Guid, long> result =
            CollaboratorDistributionCalculator.CalculateMinorUnitAmounts(
                100_000,
                2_000,
                [(Guid.NewGuid(), 6_000)]);

        Assert.Equal(12_000, Assert.Single(result).Value);
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

    [Fact]
    public void CollaboratorContribution_IsPositiveEditableAndLogicallyDeletableCapital()
    {
        Guid collaboratorId = Guid.NewGuid();
        CollaboratorContribution contribution = CollaboratorContribution.Create(
            collaboratorId,
            new DateOnly(2026, 7, 1),
            Money.FromDecimal(100m),
            "Capital inicial",
            UtcNow);

        contribution.Update(
            new DateOnly(2026, 7, 2),
            Money.FromDecimal(125m),
            "Capital corregido",
            UtcNow.AddMinutes(1));
        contribution.MarkDeleted(UtcNow.AddMinutes(2));

        Assert.Equal(collaboratorId, contribution.CollaboratorId);
        Assert.Equal(12_500, contribution.Amount.MinorUnits);
        Assert.Equal("Capital corregido", contribution.Description);
        Assert.True(contribution.IsDeleted);
        Assert.Throws<ArgumentOutOfRangeException>(() => CollaboratorContribution.Create(
            collaboratorId, new DateOnly(2026, 7, 1), Money.FromDecimal(0m), null, UtcNow));
    }
}
