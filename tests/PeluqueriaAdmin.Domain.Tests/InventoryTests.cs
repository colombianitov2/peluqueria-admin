using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class InventoryTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Date = new(2026, 7, 1);

    [Fact]
    public void InitialPurchaseSaleAndConsumption_UpdateStockAndCashExactly()
    {
        Product product = Product.Create("Agua", ProductCategory.ProductForSale, "unidad", UtcNow);
        InventoryMovement initial = InventoryMovement.Initial(
            product.Id, Date, Quantity.Positive(10m), Money.FromDecimal(100m), UtcNow);
        InventoryMovement purchase = InventoryMovement.Purchase(
            product.Id, Date.AddDays(1), Quantity.Positive(5m), Money.FromDecimal(60m), UtcNow.AddMinutes(1));
        Money averageCost = InventoryCalculator.AverageUnitCost([initial, purchase]);
        InventoryMovement sale = InventoryMovement.Sale(
            product.Id,
            Date.AddDays(2),
            Quantity.Positive(3m),
            Money.FromDecimal(20m),
            averageCost,
            15m,
            UtcNow.AddMinutes(2));
        InventoryMovement consumption = InventoryMovement.Consumption(
            product.Id, Date.AddDays(3), Quantity.Positive(2m), 12m, UtcNow.AddMinutes(3));

        InventoryMovement[] movements = [initial, purchase, sale, consumption];
        InventoryCalculator.EnsureNonNegative(movements);

        Assert.Equal(10m, InventoryCalculator.CurrentQuantity(movements));
        Assert.Equal(1_067, averageCost.MinorUnits);
        Assert.Equal(6_000, sale.CashAmount!.Value.MinorUnits);
        Assert.Equal(3_201, sale.EstimatedCost!.Value.MinorUnits);
        Assert.Equal(6_000, purchase.CashAmount!.Value.MinorUnits);
    }

    [Fact]
    public void PhysicalCount_AdjustsToExactQuantityWithoutCreatingExpense()
    {
        Guid productId = Guid.NewGuid();
        InventoryMovement initial = InventoryMovement.Initial(
            productId, Date, Quantity.Positive(10m), Money.FromDecimal(50m), UtcNow);
        InventoryMovement count = InventoryMovement.PhysicalCount(
            productId, Date.AddDays(30), Quantity.NonNegative(8m), 10m, UtcNow.AddDays(30));

        Assert.Equal(-2m, count.QuantityDelta);
        Assert.Null(count.CashAmount);
        Assert.Equal(8m, InventoryCalculator.CurrentQuantity([initial, count]));
    }

    [Fact]
    public void MonthlyRestock_UsesSurplusAndHasNoPermanentTarget()
    {
        var month = new YearMonth(2026, 8);
        MonthlyRestockPlan plan = MonthlyRestockPlan.Create(
            Guid.NewGuid(), month, Quantity.Positive(12m), UtcNow);

        Assert.Equal(4m, plan.SuggestedPurchase(8m));
        Assert.Equal(0m, plan.SuggestedPurchase(15m));
    }

    [Fact]
    public void SaleAndConsumption_RejectNegativeInventory()
    {
        Guid productId = Guid.NewGuid();

        Assert.Throws<InvalidOperationException>(() => InventoryMovement.Sale(
            productId,
            Date,
            Quantity.Positive(2m),
            Money.FromDecimal(5m),
            Money.FromDecimal(2m),
            1m,
            UtcNow));
        Assert.Throws<InvalidOperationException>(() => InventoryMovement.Consumption(
            productId, Date, Quantity.Positive(2m), 1m, UtcNow));
    }

    [Fact]
    public void Quantity_RejectsMoreThanThreeDecimals()
    {
        Assert.Throws<ArgumentException>(() => Quantity.Positive(1.0001m));
    }
}
