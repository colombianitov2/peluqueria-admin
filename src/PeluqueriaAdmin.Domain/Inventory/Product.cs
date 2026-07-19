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
        string? description = null) : base(id, utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        Category = category;
        UnitOfMeasure = NormalizeRequiredText(unitOfMeasure, nameof(unitOfMeasure));
        DefaultSalePrice = ValidateSalePrice(category, defaultSalePrice);
        Description = NormalizeOptionalText(description);
    }

    public string Name { get; private set; } = string.Empty;

    public ProductCategory Category { get; private set; }

    public string UnitOfMeasure { get; private set; } = string.Empty;

    public Money? DefaultSalePrice { get; private set; }

    public string? Description { get; private set; }

    public bool IsForSale => Category is ProductCategory.FoodOrDrinkForSale or ProductCategory.OtherProductForSale;

    public static Product Create(
        string name,
        ProductCategory category,
        string unitOfMeasure,
        DateTime utcNow,
        Money? defaultSalePrice = null,
        string? description = null) => new(
            Guid.NewGuid(), name, category, unitOfMeasure, utcNow, defaultSalePrice, description);

    public void Update(
        string name,
        ProductCategory category,
        string unitOfMeasure,
        DateTime utcNow,
        Money? defaultSalePrice = null,
        string? description = null)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        Category = category;
        UnitOfMeasure = NormalizeRequiredText(unitOfMeasure, nameof(unitOfMeasure));
        DefaultSalePrice = ValidateSalePrice(category, defaultSalePrice);
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
}
