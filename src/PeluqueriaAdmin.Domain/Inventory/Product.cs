using PeluqueriaAdmin.Domain.Common;

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
        DateTime utcNow) : base(id, utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        Category = category;
        UnitOfMeasure = NormalizeRequiredText(unitOfMeasure, nameof(unitOfMeasure));
    }

    public string Name { get; private set; } = string.Empty;

    public ProductCategory Category { get; private set; }

    public string UnitOfMeasure { get; private set; } = string.Empty;

    public static Product Create(
        string name,
        ProductCategory category,
        string unitOfMeasure,
        DateTime utcNow) => new(Guid.NewGuid(), name, category, unitOfMeasure, utcNow);

    public void Update(
        string name,
        ProductCategory category,
        string unitOfMeasure,
        DateTime utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        Category = category;
        UnitOfMeasure = NormalizeRequiredText(unitOfMeasure, nameof(unitOfMeasure));
        MarkUpdated(utcNow);
    }
}
