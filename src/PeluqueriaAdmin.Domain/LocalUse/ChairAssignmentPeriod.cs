using PeluqueriaAdmin.Domain.Common;

namespace PeluqueriaAdmin.Domain.LocalUse;

public sealed class ChairAssignmentPeriod : AuditableEntity
{
    private ChairAssignmentPeriod()
    {
    }

    private ChairAssignmentPeriod(
        Guid id,
        Guid chairId,
        Guid personId,
        DateOnly startDate,
        DateTime utcNow) : base(id, utcNow)
    {
        if (chairId == Guid.Empty) throw new ArgumentException("La silla es obligatoria.", nameof(chairId));
        if (personId == Guid.Empty) throw new ArgumentException("El trabajador es obligatorio.", nameof(personId));
        ChairId = chairId;
        PersonId = personId;
        StartDate = startDate;
    }

    public Guid ChairId { get; private set; }

    public Guid PersonId { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly? EndDateExclusive { get; private set; }

    public static ChairAssignmentPeriod Create(
        Guid chairId,
        Guid personId,
        DateOnly startDate,
        DateTime utcNow) =>
        new(Guid.NewGuid(), chairId, personId, startDate, utcNow);

    public bool AppliesOn(DateOnly date) =>
        !IsDeleted && StartDate <= date && (!EndDateExclusive.HasValue || date < EndDateExclusive.Value);

    public void Close(DateOnly endDateExclusive, DateTime utcNow)
    {
        if (endDateExclusive < StartDate)
        {
            throw new ArgumentException(
                "La finalización de la asignación no puede preceder su inicio.",
                nameof(endDateExclusive));
        }

        if (EndDateExclusive.HasValue && EndDateExclusive.Value <= endDateExclusive)
        {
            return;
        }

        EndDateExclusive = endDateExclusive;
        MarkUpdated(utcNow);
    }
}
