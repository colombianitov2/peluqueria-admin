using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Finance;

public sealed class UnofficialExpense : AuditableEntity
{
    private UnofficialExpense()
    {
    }

    private UnofficialExpense(
        Guid id,
        string name,
        Money monthlyAmount,
        DateOnly effectiveFrom,
        string? description,
        DateTime utcNow) : base(id, utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        MonthlyAmount = EnsurePositive(monthlyAmount);
        EffectiveFrom = effectiveFrom;
        Description = NormalizeOptionalText(description);
    }

    public string Name { get; private set; } = string.Empty;

    public Money MonthlyAmount { get; private set; }

    public DateOnly EffectiveFrom { get; private set; }

    public string? Description { get; private set; }

    public static UnofficialExpense Create(
        string name,
        Money monthlyAmount,
        DateOnly effectiveFrom,
        string? description,
        DateTime utcNow) => new(Guid.NewGuid(), name, monthlyAmount, effectiveFrom, description, utcNow);

    public bool AppliesOn(DateOnly date) => !IsDeleted && EffectiveFrom <= date;

    public void Update(
        string name,
        Money monthlyAmount,
        DateOnly effectiveFrom,
        string? description,
        DateTime utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        MonthlyAmount = EnsurePositive(monthlyAmount);
        EffectiveFrom = effectiveFrom;
        Description = NormalizeOptionalText(description);
        MarkUpdated(utcNow);
    }

    private static Money EnsurePositive(Money amount) => amount.MinorUnits > 0
        ? amount
        : throw new ArgumentOutOfRangeException(nameof(amount), "El valor mensual debe ser mayor que cero.");
}
