using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.LocalUse;

public sealed class DailyCharge : AuditableEntity
{
    private DailyCharge()
    {
    }

    private DailyCharge(
        Guid id,
        Guid personId,
        Guid chairId,
        Guid rateId,
        DateOnly chargeDate,
        DateOnly dueDate,
        Money amount,
        DateTime utcNow) : base(id, utcNow)
    {
        PersonId = personId;
        ChairId = chairId;
        RateId = rateId;
        ChargeDate = chargeDate;
        DueDate = dueDate;
        Amount = amount;
    }

    public Guid PersonId { get; private set; }

    public Guid ChairId { get; private set; }

    public Guid RateId { get; private set; }

    public DateOnly ChargeDate { get; private set; }

    public DateOnly DueDate { get; private set; }

    public Money Amount { get; private set; }

    internal static DailyCharge Create(
        Guid personId,
        Guid chairId,
        Guid rateId,
        DateOnly chargeDate,
        Money amount,
        DateTime utcNow) =>
        new(
            Guid.NewGuid(),
            personId,
            chairId,
            rateId,
            chargeDate,
            DueSaturday(chargeDate),
            amount,
            utcNow);

    public static DateOnly DueSaturday(DateOnly date)
    {
        int days = ((int)DayOfWeek.Saturday - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(days);
    }
}
