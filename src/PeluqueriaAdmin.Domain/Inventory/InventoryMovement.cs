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
        DateTime utcNow,
        string? description = null) : base(id, utcNow)
    {
        ProductId = productId;
        Date = date;
        Type = type;
        QuantityDelta = quantityDelta;
        CashAmount = cashAmount;
        EstimatedCost = estimatedCost;
        Description = NormalizeOptionalText(description);
    }

    public Guid ProductId { get; private set; }

    public DateOnly Date { get; private set; }

    public InventoryMovementType Type { get; private set; }

    public decimal QuantityDelta { get; private set; }

    public Money? CashAmount { get; private set; }

    public Money? EstimatedCost { get; private set; }

    public string? Description { get; private set; }

    public void Correct(
        DateOnly date,
        decimal quantityDelta,
        Money? cashAmount,
        Money? estimatedCost,
        DateTime utcNow,
        string? description = null)
    {
        ValidateCorrection(Type, quantityDelta, cashAmount, estimatedCost);

        Date = date;
        QuantityDelta = quantityDelta;
        CashAmount = cashAmount;
        EstimatedCost = estimatedCost;
        Description = NormalizeOptionalText(description);
        MarkUpdated(utcNow);
    }

    private static void ValidateCorrection(
        InventoryMovementType type,
        decimal quantityDelta,
        Money? cashAmount,
        Money? estimatedCost)
    {
        if (decimal.Round(quantityDelta, 3) != quantityDelta)
        {
            throw new ArgumentException("La cantidad no puede tener más de tres decimales.", nameof(quantityDelta));
        }

        if (type is InventoryMovementType.InitialStock or InventoryMovementType.Purchase && quantityDelta <= 0m)
        {
            throw new InvalidOperationException("La existencia inicial y la compra deben tener cantidad positiva.");
        }

        if (type is InventoryMovementType.Sale or InventoryMovementType.InternalConsumption && quantityDelta >= 0m)
        {
            throw new InvalidOperationException("La venta y el consumo deben reducir la existencia.");
        }

        if (type is InventoryMovementType.Purchase or InventoryMovementType.Sale
            && (!cashAmount.HasValue || cashAmount.Value.MinorUnits <= 0))
        {
            throw new InvalidOperationException("La compra y la venta requieren un importe monetario positivo.");
        }

        if (type == InventoryMovementType.InitialStock && cashAmount.HasValue)
        {
            throw new InvalidOperationException("La existencia inicial no genera un movimiento de caja.");
        }

        if (type is InventoryMovementType.InternalConsumption or InventoryMovementType.PhysicalCountAdjustment
            && (cashAmount.HasValue || estimatedCost.HasValue))
        {
            throw new InvalidOperationException("El consumo y el conteo físico no generan movimientos de caja ni costos nuevos.");
        }
    }

    public static InventoryMovement Initial(
        Guid productId,
        DateOnly date,
        Quantity quantity,
        Money totalCost,
        DateTime utcNow,
        string? description = null) => new(
            Guid.NewGuid(), productId, date, InventoryMovementType.InitialStock,
            quantity.Value, null, totalCost, utcNow, description);

    public static InventoryMovement Purchase(
        Guid productId,
        DateOnly date,
        Quantity quantity,
        Money totalCost,
        DateTime utcNow,
        string? description = null) => new(
            Guid.NewGuid(), productId, date, InventoryMovementType.Purchase,
            quantity.Value, totalCost, totalCost, utcNow, description);

    public static InventoryMovement Sale(
        Guid productId,
        DateOnly date,
        Quantity quantity,
        Money unitPrice,
        Money estimatedUnitCost,
        decimal availableQuantity,
        DateTime utcNow,
        string? description = null)
    {
        EnsureAvailable(quantity, availableQuantity);
        return new InventoryMovement(
            Guid.NewGuid(), productId, date, InventoryMovementType.Sale,
            -quantity.Value,
            Multiply(unitPrice, quantity),
            Multiply(estimatedUnitCost, quantity),
            utcNow,
            description);
    }

    public static InventoryMovement Consumption(
        Guid productId,
        DateOnly date,
        Quantity quantity,
        decimal availableQuantity,
        DateTime utcNow,
        string? description = null)
    {
        EnsureAvailable(quantity, availableQuantity);
        return new InventoryMovement(
            Guid.NewGuid(), productId, date, InventoryMovementType.InternalConsumption,
            -quantity.Value, null, null, utcNow, description);
    }

    public static InventoryMovement PhysicalCount(
        Guid productId,
        DateOnly date,
        Quantity physicalQuantity,
        decimal currentQuantity,
        DateTime utcNow,
        string? description = null) => new(
            Guid.NewGuid(), productId, date, InventoryMovementType.PhysicalCountAdjustment,
            physicalQuantity.Value - currentQuantity, null, null, utcNow, description);

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
