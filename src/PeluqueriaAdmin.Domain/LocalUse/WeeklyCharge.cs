using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.LocalUse;

public sealed class WeeklyCharge : AuditableEntity
{
    private WeeklyCharge()
    {
    }

    private WeeklyCharge(
        Guid id,
        Guid personId,
        DateOnly periodStart,
        Money amount,
        DateTime utcNow) : base(id, utcNow)
    {
        PersonId = personId;
        PeriodStart = periodStart;
        PeriodEnd = periodStart.AddDays(6);
        Amount = amount;
    }

    public Guid PersonId { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public Money Amount { get; private set; }

    internal static WeeklyCharge Create(
        Guid personId,
        DateOnly periodStart,
        Money amount,
        DateTime utcNow) => new(Guid.NewGuid(), personId, periodStart, amount, utcNow);
}
