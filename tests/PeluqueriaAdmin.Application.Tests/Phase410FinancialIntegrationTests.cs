using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Tests;

public sealed class Phase410FinancialIntegrationTests
{
    private static readonly DateTime UtcNow =
        new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);
    private static readonly YearMonth July = new(2026, 7);

    [Fact]
    public void MonthlyPurchase_IsSharedByHomeBreakEvenAndSuggestedChairPrice_WithoutDoubleCounting()
    {
        MonthlyPurchaseItem plan = MonthlyPurchaseItem.Create(
            "Champú",
            ProductCategory.OtherProductForSale,
            July,
            2,
            Money.FromDecimal(12.50m),
            false,
            true,
            UtcNow);
        AdministrationData pendingData = EmptyData() with { MonthlyPurchaseItems = [plan] };

        HomeDashboard pendingHome = HomeDashboardCalculator.Calculate(
            pendingData,
            Percentage.FromPercent(0),
            new DateOnly(2026, 7, 23));
        FinancialMonthSnapshot pendingMonth = FinancialMonthCalculator.Calculate(
            pendingData,
            Percentage.FromPercent(0),
            July);
        SuggestedChairPrice pendingPrice = SuggestedChairPriceCalculator.Calculate(
            pendingData,
            Money.FromDecimal(12),
            July,
            new DateOnly(2026, 7, 23));

        PendingHomeObligation homeRow = Assert.Single(pendingHome.Obligations);
        Assert.Equal("Compra mensual", homeRow.Type);
        Assert.Equal(2_500, homeRow.Amount.MinorUnits);
        Assert.Equal(2_500, pendingMonth.AccountsPayableMinorUnits);
        Assert.Equal(2_500, pendingMonth.NewReservesMinorUnits);
        Assert.Equal(2_500, pendingMonth.BreakEvenMinorUnits);
        Assert.Equal(2_500, pendingPrice.OfficialGoalMinorUnits);

        Product product = Product.Create(
            plan.Name,
            plan.Category,
            "unidad",
            UtcNow,
            Money.FromDecimal(20),
            defaultUnitCost: plan.ExpectedUnitCost);
        InventoryMovement purchase = InventoryMovement.Purchase(
            product.Id,
            new DateOnly(2026, 7, 24),
            Quantity.Positive(2),
            Money.FromDecimal(25),
            UtcNow);
        plan.LinkInventoryProduct(product.Id, purchase.Id, UtcNow.AddMinutes(1));
        AdministrationData purchasedData = EmptyData() with
        {
            Products = [product],
            InventoryMovements = [purchase],
            MonthlyPurchaseItems = [plan],
        };

        HomeDashboard purchasedHome = HomeDashboardCalculator.Calculate(
            purchasedData,
            Percentage.FromPercent(0),
            new DateOnly(2026, 7, 24));
        FinancialMonthSnapshot purchasedMonth = FinancialMonthCalculator.Calculate(
            purchasedData,
            Percentage.FromPercent(0),
            July);

        Assert.Empty(purchasedHome.Obligations);
        Assert.Equal(0, purchasedMonth.AccountsPayableMinorUnits);
        Assert.Equal(0, purchasedMonth.NewReservesMinorUnits);
        Assert.Equal(2_500, purchasedMonth.PaidOutflowsMinorUnits);
        Assert.Equal(2_500, purchasedMonth.BreakEvenMinorUnits);
    }

    [Fact]
    public void SettledCredit_UsesActualPaymentAndLeavesNoHomeOrPayableDifference()
    {
        Obligation credit = Obligation.Create(
            "Crédito comercial",
            ObligationType.Credit,
            new DateOnly(2026, 7, 23),
            Money.FromDecimal(100),
            RecurrenceFrequency.Weekly,
            UtcNow);
        ObligationPayment payment = ObligationPayment.Create(
            credit.Id,
            new DateOnly(2026, 7, 23),
            Money.FromDecimal(80),
            UtcNow);
        credit.MarkSettled(UtcNow.AddMinutes(1));
        AdministrationData data = EmptyData() with
        {
            Obligations = [credit],
            ObligationPayments = [payment],
        };

        HomeDashboard home = HomeDashboardCalculator.Calculate(
            data,
            Percentage.FromPercent(0),
            new DateOnly(2026, 7, 23));
        FinancialMonthSnapshot month = FinancialMonthCalculator.Calculate(
            data,
            Percentage.FromPercent(0),
            July);

        Assert.Empty(home.Obligations);
        Assert.Equal(0, month.AccountsPayableMinorUnits);
        Assert.Equal(0, month.NewReservesMinorUnits);
        Assert.Equal(8_000, month.PaidOutflowsMinorUnits);
        Assert.Equal(8_000, month.BreakEvenMinorUnits);
    }

    private static AdministrationData EmptyData() => new(
        [], [], [], [], [], [], [], [], [], [], [], [], [], [], []);
}
