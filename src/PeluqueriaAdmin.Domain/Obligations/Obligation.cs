using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Obligations;

public sealed class Obligation : AuditableEntity
{
    private Obligation()
    {
    }

    private Obligation(
        Guid id,
        Guid seriesId,
        string name,
        ObligationType type,
        DateOnly dueDate,
        Money expectedAmount,
        RecurrenceFrequency recurrence,
        DateTime utcNow) : base(id, utcNow)
    {
        SeriesId = seriesId;
        Name = NormalizeRequiredText(name, nameof(name));
        Type = type;
        DueDate = dueDate;
        ExpectedAmount = expectedAmount.MinorUnits > 0
            ? expectedAmount
            : throw new ArgumentOutOfRangeException(nameof(expectedAmount), "El importe esperado debe ser mayor que cero.");
        Recurrence = recurrence;
    }

    public Guid SeriesId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public ObligationType Type { get; private set; }

    public DateOnly DueDate { get; private set; }

    public Money ExpectedAmount { get; private set; }

    public RecurrenceFrequency Recurrence { get; private set; }

    public static Obligation Create(
        string name,
        ObligationType type,
        DateOnly dueDate,
        Money expectedAmount,
        RecurrenceFrequency recurrence,
        DateTime utcNow) => new(
            Guid.NewGuid(), Guid.NewGuid(), name, type, dueDate, expectedAmount, recurrence, utcNow);

    public void Update(
        string name,
        ObligationType type,
        DateOnly dueDate,
        Money expectedAmount,
        RecurrenceFrequency recurrence,
        DateTime utcNow)
    {
        if (expectedAmount.MinorUnits == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedAmount));
        }

        Name = NormalizeRequiredText(name, nameof(name));
        Type = type;
        DueDate = dueDate;
        ExpectedAmount = expectedAmount;
        Recurrence = recurrence;
        MarkUpdated(utcNow);
    }

    internal static Obligation CreateOccurrence(Obligation template, DateOnly dueDate, DateTime utcNow) =>
        new(
            Guid.NewGuid(),
            template.SeriesId,
            template.Name,
            template.Type,
            dueDate,
            template.ExpectedAmount,
            template.Recurrence,
            utcNow);

    public Money GoalAmount(IEnumerable<ObligationPayment> payments)
    {
        long paid = TotalPaidMinorUnits(payments);
        return paid >= ExpectedAmount.MinorUnits
            ? Money.FromMinorUnits(paid)
            : ExpectedAmount;
    }

    public ObligationStatus Status(IEnumerable<ObligationPayment> payments, DateOnly today)
    {
        long paid = TotalPaidMinorUnits(payments);
        if (paid >= ExpectedAmount.MinorUnits)
        {
            return ObligationStatus.Paid;
        }

        if (DueDate < today)
        {
            return ObligationStatus.Overdue;
        }

        return paid > 0 ? ObligationStatus.Partial : ObligationStatus.Pending;
    }

    private long TotalPaidMinorUnits(IEnumerable<ObligationPayment> payments) => payments
        .Where(payment => !payment.IsDeleted && payment.ObligationId == Id)
        .Sum(payment => payment.Amount.MinorUnits);
}
