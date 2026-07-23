using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Inventory;

public sealed class MonthlyPurchaseItem : AuditableEntity
{
    private MonthlyPurchaseItem() { }

    private MonthlyPurchaseItem(Guid id, Guid productId, YearMonth month, decimal quantity,
        Money expectedUnitCost, bool isActive, bool reserveWhenOutOfStock, DateTime utcNow,
        string? description) : base(id, utcNow)
    {
        ProductId = productId;
        Month = month;
        SetValues(quantity, expectedUnitCost, isActive, reserveWhenOutOfStock, description);
    }

    public Guid ProductId { get; private set; }
    public YearMonth Month { get; private set; }
    public decimal Quantity { get; private set; }
    public Money ExpectedUnitCost { get; private set; }
    public bool IsActive { get; private set; }
    public bool ReserveWhenOutOfStock { get; private set; }
    public string? Description { get; private set; }
    public Guid? PurchaseMovementId { get; private set; }
    public long ExpectedTotalMinorUnits => checked((long)decimal.Round(
        Quantity * ExpectedUnitCost.MinorUnits, 0, MidpointRounding.AwayFromZero));

    public static MonthlyPurchaseItem Create(Guid productId, YearMonth month, decimal quantity,
        Money expectedUnitCost, bool isActive, bool reserveWhenOutOfStock, DateTime utcNow,
        string? description = null) => new(Guid.NewGuid(), productId, month, quantity,
            expectedUnitCost, isActive, reserveWhenOutOfStock, utcNow, description);

    public void Update(decimal quantity, Money expectedUnitCost, bool isActive,
        bool reserveWhenOutOfStock, DateTime utcNow, string? description = null)
    {
        if (PurchaseMovementId.HasValue)
            throw new InvalidOperationException("Una compra vinculada conserva su fotografía histórica.");
        SetValues(quantity, expectedUnitCost, isActive, reserveWhenOutOfStock, description);
        MarkUpdated(utcNow);
    }

    public void LinkPurchase(Guid movementId, DateTime utcNow)
    {
        if (movementId == Guid.Empty) throw new ArgumentException("El movimiento no puede estar vacío.", nameof(movementId));
        if (PurchaseMovementId.HasValue) throw new InvalidOperationException("La compra mensual ya está vinculada.");
        PurchaseMovementId = movementId;
        MarkUpdated(utcNow);
    }

    private void SetValues(decimal quantity, Money expectedUnitCost, bool isActive,
        bool reserveWhenOutOfStock, string? description)
    {
        if (quantity <= 0 || decimal.Round(quantity, 3) != quantity)
            throw new ArgumentOutOfRangeException(nameof(quantity), "La cantidad debe ser positiva y tener máximo tres decimales.");
        if (expectedUnitCost.MinorUnits <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedUnitCost), "El costo esperado debe ser mayor que cero.");
        Quantity = quantity;
        ExpectedUnitCost = expectedUnitCost;
        IsActive = isActive;
        ReserveWhenOutOfStock = reserveWhenOutOfStock;
        Description = NormalizeOptionalText(description);
    }
}
