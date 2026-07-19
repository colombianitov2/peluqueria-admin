using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Inventory;

public sealed class InventoryMovement : AuditableEntity
{
    private InventoryMovement()
    {
    }

    private InventoryMovement(
        Guid id,
        Guid productId,
        DateOnly date,
        InventoryMovementType type,
        decimal quantityDelta,
        Money? cashAmount,
        Money? estimatedCost,
        DateTime utcNow) : base(id, utcNow)
    {
        ProductId = productId;
        Date = date;
        Type = type;
        QuantityDelta = quantityDelta;
        CashAmount = cashAmount;
        EstimatedCost = estimatedCost;
    }

    public Guid ProductId { get; private set; }

    public DateOnly Date { get; private set; }

    public InventoryMovementType Type { get; private set; }

    public decimal QuantityDelta { get; private set; }

    public Money? CashAmount { get; private set; }

    public Money? EstimatedCost { get; private set; }

    public void Correct(
        DateOnly date,
        decimal quantityDelta,
        Money? cashAmount,
        Money? estimatedCost,
        DateTime utcNow)
    {
        if (decimal.Round(quantityDelta, 3) != quantityDelta)
        {
            throw new ArgumentException("La cantidad no puede tener más de tres decimales.", nameof(quantityDelta));
        }

        Date = date;
        QuantityDelta = quantityDelta;
        CashAmount = cashAmount;
        EstimatedCost = estimatedCost;
        MarkUpdated(utcNow);
    }

    public static InventoryMovement Initial(
        Guid productId,
        DateOnly date,
        Quantity quantity,
        Money totalCost,
        DateTime utcNow) => new(
            Guid.NewGuid(), productId, date, InventoryMovementType.InitialStock,
            quantity.Value, null, totalCost, utcNow);

    public static InventoryMovement Purchase(
        Guid productId,
        DateOnly date,
        Quantity quantity,
        Money totalCost,
        DateTime utcNow) => new(
            Guid.NewGuid(), productId, date, InventoryMovementType.Purchase,
            quantity.Value, totalCost, totalCost, utcNow);

    public static InventoryMovement Sale(
        Guid productId,
        DateOnly date,
        Quantity quantity,
        Money unitPrice,
        Money estimatedUnitCost,
        decimal availableQuantity,
        DateTime utcNow)
    {
        EnsureAvailable(quantity, availableQuantity);
        return new InventoryMovement(
            Guid.NewGuid(), productId, date, InventoryMovementType.Sale,
            -quantity.Value,
            Multiply(unitPrice, quantity),
            Multiply(estimatedUnitCost, quantity),
            utcNow);
    }

    public static InventoryMovement Consumption(
        Guid productId,
        DateOnly date,
        Quantity quantity,
        decimal availableQuantity,
        DateTime utcNow)
    {
        EnsureAvailable(quantity, availableQuantity);
        return new InventoryMovement(
            Guid.NewGuid(), productId, date, InventoryMovementType.InternalConsumption,
            -quantity.Value, null, null, utcNow);
    }

    public static InventoryMovement PhysicalCount(
        Guid productId,
        DateOnly date,
        Quantity physicalQuantity,
        decimal currentQuantity,
        DateTime utcNow) => new(
            Guid.NewGuid(), productId, date, InventoryMovementType.PhysicalCountAdjustment,
            physicalQuantity.Value - currentQuantity, null, null, utcNow);

    private static Money Multiply(Money unitAmount, Quantity quantity)
    {
        decimal minorUnits = unitAmount.MinorUnits * quantity.Value;
        return Money.FromMinorUnits(checked((long)decimal.Round(minorUnits, 0, MidpointRounding.AwayFromZero)));
    }

    private static void EnsureAvailable(Quantity quantity, decimal availableQuantity)
    {
        if (quantity.Value > availableQuantity)
        {
            throw new InvalidOperationException("La operación dejaría el inventario en negativo.");
        }
    }
}
