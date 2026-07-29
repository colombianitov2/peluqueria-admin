using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.LocalUse;

public sealed class DailyRate : AuditableEntity
{
    private DailyRate()
    {
    }

    private DailyRate(
        Guid id,
        DateOnly effectiveDate,
        DateTime effectiveFromUtc,
        Money amount,
        DateTime utcNow) : base(id, utcNow)
    {
        EnsureUtc(effectiveFromUtc);
        EffectiveDate = effectiveDate;
        EffectiveFromUtc = effectiveFromUtc;
        Amount = amount;
    }

    public DateOnly EffectiveDate { get; private set; }

    public DateTime EffectiveFromUtc { get; private set; }

    public DateTime? EffectiveToUtc { get; private set; }

    public DateOnly? EffectiveToDateExclusive { get; private set; }

    public Money Amount { get; private set; }

    public static DailyRate Create(
        DateOnly effectiveDate,
        DateTime effectiveFromUtc,
        Money amount,
        DateTime utcNow) =>
        new(Guid.NewGuid(), effectiveDate, effectiveFromUtc, amount, utcNow);

    public void Close(DateOnly effectiveToDateExclusive, DateTime effectiveToUtc)
    {
        EnsureUtc(effectiveToUtc);
        if (effectiveToDateExclusive < EffectiveDate)
        {
            throw new ArgumentException(
                "La fecha final de una tarifa no puede preceder su inicio.",
                nameof(effectiveToDateExclusive));
        }
        if (effectiveToUtc < EffectiveFromUtc)
        {
            throw new ArgumentException(
                "La finalización de una tarifa no puede preceder su inicio.",
                nameof(effectiveToUtc));
        }

        EffectiveToDateExclusive = effectiveToDateExclusive;
        EffectiveToUtc = effectiveToUtc;
        MarkUpdated(effectiveToUtc);
    }

    public bool AppliesOn(DateOnly date) =>
        EffectiveDate <= date
        && (!EffectiveToDateExclusive.HasValue || date < EffectiveToDateExclusive.Value);
}
