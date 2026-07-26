using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Inventory;

public sealed class Product : AuditableEntity
{
    private Product()
    {
    }

    private Product(
        Guid id,
        string name,
        ProductCategory category,
        string unitOfMeasure,
        DateTime utcNow,
        Money? defaultSalePrice = null,
        string? description = null,
        Money? defaultUnitCost = null) : base(id, utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        Category = category;
        UnitOfMeasure = NormalizeRequiredText(unitOfMeasure, nameof(unitOfMeasure));
        DefaultSalePrice = ValidateSalePrice(category, defaultSalePrice);
        DefaultUnitCost = ValidateUnitCost(defaultUnitCost);
        Description = NormalizeOptionalText(description);
    }

    public string Name { get; private set; } = string.Empty;

    public ProductCategory Category { get; private set; }

    public string UnitOfMeasure { get; private set; } = string.Empty;

    public Money? DefaultSalePrice { get; private set; }

    public Money? DefaultUnitCost { get; private set; }

    public string? Description { get; private set; }

    public bool IsForSale => Category is ProductCategory.FoodOrDrinkForSale or ProductCategory.OtherProductForSale;

    public static Product Create(
        string name,
        ProductCategory category,
        string unitOfMeasure,
        DateTime utcNow,
        Money? defaultSalePrice = null,
        string? description = null,
        Money? defaultUnitCost = null) => new(
            Guid.NewGuid(), name, category, unitOfMeasure, utcNow, defaultSalePrice, description, defaultUnitCost);

    public void Update(
        string name,
        ProductCategory category,
        string unitOfMeasure,
        DateTime utcNow,
        Money? defaultSalePrice = null,
        string? description = null,
        Money? defaultUnitCost = null)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        Category = category;
        UnitOfMeasure = NormalizeRequiredText(unitOfMeasure, nameof(unitOfMeasure));
        DefaultSalePrice = ValidateSalePrice(category, defaultSalePrice);
        DefaultUnitCost = ValidateUnitCost(defaultUnitCost);
        Description = NormalizeOptionalText(description);
        MarkUpdated(utcNow);
    }

    private static Money? ValidateSalePrice(ProductCategory category, Money? price)
    {
        bool forSale = category is ProductCategory.FoodOrDrinkForSale or ProductCategory.OtherProductForSale;
        if (price.HasValue && price.Value.MinorUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "El precio de venta debe ser mayor que cero.");
        }

        return forSale ? price : null;
    }

    private static Money? ValidateUnitCost(Money? cost)
    {
        if (cost.HasValue && cost.Value.MinorUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cost), "El costo configurado debe ser mayor que cero.");
        }

        return cost;
    }
}
