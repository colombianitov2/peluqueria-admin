using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Localization;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Tests;

public sealed class AdministrationReportReconciliationTests
{
    private static readonly DateTime UtcNow =
        new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly YearMonth July = new(2026, 7);

    [Fact]
    public void MonthlyExpenses_AssignsPendingPlansToTheirOwnMonth_IgnoringLegacyFlags()
    {
        MonthlyPurchaseItem julyPlan = MonthlyPurchaseItem.Create(
            "Champú pendiente",
            ProductCategory.OtherProductForSale,
            July,
            2,
            Money.FromDecimal(12.50m),
            false,
            false,
            UtcNow);
        MonthlyPurchaseItem augustPlan = MonthlyPurchaseItem.Create(
            "Champú futuro",
            ProductCategory.OtherProductForSale,
            new YearMonth(2026, 8),
            3,
            Money.FromDecimal(10m),
            true,
            true,
            UtcNow);
        AdministrationData data = EmptyData() with
        {
            MonthlyPurchaseItems = [julyPlan, augustPlan],
        };

        MonthlyExpenseBreakdown result = AdministrationReports.MonthlyExpenses(data, July);

        Assert.Equal(2_500, result.PendingPlansMinorUnits);
        Assert.Equal(2_500, result.TotalMinorUnits);
    }

    [Fact]
    public void MonthlyExpenses_ReplacesLinkedPlanWithActualPurchase_WithoutDoubleCounting()
    {
        Product product = Product.Create(
            "Tinte",
            ProductCategory.OtherProductForSale,
            "unidad",
            UtcNow);
        MonthlyPurchaseItem plan = MonthlyPurchaseItem.Create(
            product.Id,
            July,
            2,
            Money.FromDecimal(12.50m),
            false,
            false,
            UtcNow);
        InventoryMovement purchase = InventoryMovement.Purchase(
            product.Id,
            new DateOnly(2026, 7, 24),
            Quantity.Positive(2),
            Money.FromDecimal(30m),
            UtcNow);
        plan.LinkPurchase(purchase.Id, UtcNow.AddMinutes(1));
        AdministrationData data = EmptyData() with
        {
            Products = [product],
            InventoryMovements = [purchase],
            MonthlyPurchaseItems = [plan],
        };

        MonthlyExpenseBreakdown result = AdministrationReports.MonthlyExpenses(data, July);

        Assert.Equal(0, result.PendingPlansMinorUnits);
        Assert.Equal(3_000, result.MerchandiseMinorUnits);
        Assert.Equal(3_000, result.TotalMinorUnits);
    }

    [Fact]
    public void AnnualReport_ReconcilesExpenseBreakdownWithCanonicalBreakEven()
    {
        MonthlyPurchaseItem plan = MonthlyPurchaseItem.Create(
            "Champú",
            ProductCategory.OtherProductForSale,
            July,
            2,
            Money.FromDecimal(12.50m),
            false,
            false,
            UtcNow);
        AdministrationData data = EmptyData() with { MonthlyPurchaseItems = [plan] };
        long expected = Enumerable.Range(1, 12)
            .Select(month => FinancialMonthCalculator.Calculate(
                data,
                Percentage.FromPercent(0m),
                new YearMonth(2026, month)).BreakEvenMinorUnits)
            .Sum();

        AnnualAdministrationReport result = AdministrationReports.Annual(
            data,
            Percentage.FromPercent(0m),
            2026);

        Assert.Equal(2_500, result.Expenses.PendingPlansMinorUnits);
        Assert.Equal(expected, result.Expenses.TotalMinorUnits);
        Assert.Equal(expected - 2_500, result.Expenses.HistoricalAdjustmentMinorUnits);
    }

    [Fact]
    public void AnnualReport_PreservesConfirmedBreakEvenSnapshotWhenLiveDataChanges()
    {
        FinancialEntry expenseAtClose = FinancialEntry.CreateExpense(
            new DateOnly(2026, 7, 20),
            "Gasto al cierre",
            ExpenseCategory.Other,
            Money.FromDecimal(25m),
            UtcNow);
        AdministrationData dataAtClose = EmptyData() with { FinancialEntries = [expenseAtClose] };
        MonthlyClose close = MonthlyClose.Create(
            FinancialMonthCalculator.Calculate(
                dataAtClose,
                Percentage.FromPercent(0m),
                July),
            UtcNow.AddHours(1));
        FinancialEntry laterExpense = FinancialEntry.CreateExpense(
            new DateOnly(2026, 7, 25),
            "Gasto posterior",
            ExpenseCategory.Other,
            Money.FromDecimal(35m),
            UtcNow.AddHours(2));
        AdministrationData currentData = EmptyData() with
        {
            FinancialEntries = [expenseAtClose, laterExpense],
            MonthlyCloses = [close],
        };

        AnnualAdministrationReport result = AdministrationReports.Annual(
            currentData,
            Percentage.FromPercent(0m),
            2026);

        Assert.Equal(6_000, result.Expenses.OtherExpensesMinorUnits);
        Assert.Equal(-3_500, result.Expenses.HistoricalAdjustmentMinorUnits);
        Assert.Equal(2_500, result.Expenses.TotalMinorUnits);
    }

    [Fact]
    public void SpanishText_UsesExplicitLabelForNoRecurrence()
    {
        Assert.Equal("Sin recurrencia", SpanishText.For(RecurrenceFrequency.None));
    }

    private static AdministrationData EmptyData() => new(
        [], [], [], [], [], [], [], [], [], [], [], [], [], [], []);
}
