using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.LocalUse;

public sealed class WeeklyRate : AuditableEntity
{
    private WeeklyRate()
    {
    }

    private WeeklyRate(Guid id, DateOnly effectiveFrom, Money amount, DateTime utcNow)
        : base(id, utcNow)
    {
        EffectiveFrom = effectiveFrom;
        Amount = amount;
    }

    public DateOnly EffectiveFrom { get; private set; }

    public Money Amount { get; private set; }

    public static WeeklyRate Create(DateOnly effectiveFrom, Money amount, DateTime utcNow) =>
        new(Guid.NewGuid(), effectiveFrom, amount, utcNow);
}
