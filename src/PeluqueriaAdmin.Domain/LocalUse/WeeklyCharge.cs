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
        DateOnly periodEnd,
        DateOnly dueDate,
        Money amount,
        DateTime utcNow) : base(id, utcNow)
    {
        PersonId = personId;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        DueDate = dueDate;
        Amount = amount;
    }

    public Guid PersonId { get; private set; }

    public DateOnly PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    public DateOnly DueDate { get; private set; }

    public Money Amount { get; private set; }

    internal static WeeklyCharge Create(
        Guid personId,
        DateOnly periodStart,
        Money amount,
        DateTime utcNow)
    {
        DateOnly periodEnd = PaymentDueDateFor(periodStart);
        return new(
            Guid.NewGuid(),
            personId,
            periodStart,
            periodEnd,
            periodEnd,
            amount,
            utcNow);
    }

    internal static WeeklyCharge CreateForDueDate(
        Guid personId,
        DateOnly dueDate,
        Money amount,
        DateTime utcNow)
    {
        if (dueDate.DayOfWeek != DayOfWeek.Saturday)
        {
            throw new ArgumentException(
                "La cuota semanal debe vencer un sábado.",
                nameof(dueDate));
        }

        return new WeeklyCharge(
            Guid.NewGuid(),
            personId,
            dueDate.AddDays(-6),
            dueDate,
            dueDate,
            amount,
            utcNow);
    }

    internal static DateOnly PaymentDueDateFor(DateOnly date)
    {
        int days = ((int)DayOfWeek.Saturday - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(days);
    }
}
