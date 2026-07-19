using PeluqueriaAdmin.Domain.Common;

namespace PeluqueriaAdmin.Domain.Inventory;

public sealed class MonthlyRestockPlan : AuditableEntity
{
    private MonthlyRestockPlan()
    {
    }

    private MonthlyRestockPlan(
        Guid id,
        Guid productId,
        YearMonth month,
        Quantity neededQuantity,
        DateTime utcNow) : base(id, utcNow)
    {
        ProductId = productId;
        Month = month;
        NeededQuantity = neededQuantity;
    }

    public Guid ProductId { get; private set; }

    public YearMonth Month { get; private set; }

    public Quantity NeededQuantity { get; private set; }

    public static MonthlyRestockPlan Create(
        Guid productId,
        YearMonth month,
        Quantity neededQuantity,
        DateTime utcNow) => new(Guid.NewGuid(), productId, month, neededQuantity, utcNow);

    public void Update(YearMonth month, Quantity neededQuantity, DateTime utcNow)
    {
        Month = month;
        NeededQuantity = neededQuantity;
        MarkUpdated(utcNow);
    }

    public decimal SuggestedPurchase(decimal availableQuantity) =>
        Math.Max(0m, NeededQuantity.Value - availableQuantity);
}
