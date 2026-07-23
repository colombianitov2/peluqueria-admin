using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Inventory;

public sealed class MonthlyPurchaseItem : AuditableEntity
{
    private MonthlyPurchaseItem() { }

    private MonthlyPurchaseItem(Guid id, string name, ProductCategory category, Guid? productId,
        YearMonth month, decimal quantity,
        Money expectedUnitCost, bool isActive, bool reserveWhenOutOfStock, DateTime utcNow,
        string? description) : base(id, utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        Category = category;
        ProductId = productId;
        Month = month;
        SetValues(quantity, expectedUnitCost, isActive, reserveWhenOutOfStock, description);
    }

    public string Name { get; private set; } = string.Empty;
    public ProductCategory Category { get; private set; }
    public Guid? ProductId { get; private set; }
    public YearMonth Month { get; private set; }
    public decimal Quantity { get; private set; }
    public Money ExpectedUnitCost { get; private set; }
    public bool IsActive { get; private set; }
    public bool ReserveWhenOutOfStock { get; private set; }
    public string? Description { get; private set; }
    public Guid? PurchaseMovementId { get; private set; }
    public long ExpectedTotalMinorUnits => checked((long)decimal.Round(
        Quantity * ExpectedUnitCost.MinorUnits, 0, MidpointRounding.AwayFromZero));

    public static MonthlyPurchaseItem Create(string name, ProductCategory category, YearMonth month,
        decimal quantity,
        Money expectedUnitCost, bool isActive, bool reserveWhenOutOfStock, DateTime utcNow,
        string? description = null, Guid? productId = null) => new(
            Guid.NewGuid(), name, category, productId, month, quantity,
            expectedUnitCost, isActive, reserveWhenOutOfStock, utcNow, description);

    public static MonthlyPurchaseItem Create(Guid productId, YearMonth month, decimal quantity,
        Money expectedUnitCost, bool isActive, bool reserveWhenOutOfStock, DateTime utcNow,
        string? description = null) => new(
            Guid.NewGuid(), "Producto planificado", ProductCategory.OtherLocalProduct,
            productId, month, quantity, expectedUnitCost, isActive, reserveWhenOutOfStock,
            utcNow, description);

    public void Update(string name, ProductCategory category, YearMonth month, decimal quantity,
        Money expectedUnitCost, bool isActive,
        bool reserveWhenOutOfStock, DateTime utcNow, string? description = null)
    {
        if (PurchaseMovementId.HasValue)
            throw new InvalidOperationException("Una compra vinculada conserva su fotografía histórica.");
        Name = NormalizeRequiredText(name, nameof(name));
        Category = category;
        Month = month;
        SetValues(quantity, expectedUnitCost, isActive, reserveWhenOutOfStock, description);
        MarkUpdated(utcNow);
    }

    public void Update(decimal quantity, Money expectedUnitCost, bool isActive,
        bool reserveWhenOutOfStock, DateTime utcNow, string? description = null) =>
        Update(Name, Category, Month, quantity, expectedUnitCost, isActive,
            reserveWhenOutOfStock, utcNow, description);

    public void LinkInventoryProduct(Guid productId, Guid movementId, DateTime utcNow)
    {
        if (productId == Guid.Empty) throw new ArgumentException("El producto no puede estar vacío.", nameof(productId));
        if (movementId == Guid.Empty) throw new ArgumentException("El movimiento no puede estar vacío.", nameof(movementId));
        if (PurchaseMovementId.HasValue) throw new InvalidOperationException("La compra mensual ya está vinculada.");
        if (ProductId.HasValue && ProductId.Value != productId)
            throw new InvalidOperationException("El artículo mensual ya está vinculado a otro producto.");
        ProductId = productId;
        PurchaseMovementId = movementId;
        MarkUpdated(utcNow);
    }

    public void LinkPurchase(Guid movementId, DateTime utcNow)
    {
        if (!ProductId.HasValue)
            throw new InvalidOperationException("Primero debe vincularse un producto de inventario.");
        LinkInventoryProduct(ProductId.Value, movementId, utcNow);
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
