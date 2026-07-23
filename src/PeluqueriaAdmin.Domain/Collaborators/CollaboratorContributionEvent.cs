using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Collaborators;

public enum CollaboratorContributionEventType
{
    Created = 1,
    Edited = 2,
    Deleted = 3,
    MigratedSnapshot = 4,
}

public sealed class CollaboratorContributionEvent : AuditableEntity
{
    private CollaboratorContributionEvent()
    {
    }

    private CollaboratorContributionEvent(
        Guid id,
        Guid contributionId,
        Guid collaboratorId,
        CollaboratorContributionEventType eventType,
        DateOnly? previousEffectiveDate,
        DateOnly effectiveDate,
        Money? previousAmount,
        Money amount,
        string? previousDescription,
        string? description,
        DateTime utcNow) : base(id, utcNow)
    {
        if (contributionId == Guid.Empty) throw new ArgumentException("El aporte es obligatorio.", nameof(contributionId));
        if (collaboratorId == Guid.Empty) throw new ArgumentException("El colaborador es obligatorio.", nameof(collaboratorId));
        ContributionId = contributionId;
        CollaboratorId = collaboratorId;
        EventType = eventType;
        PreviousEffectiveDate = previousEffectiveDate;
        EffectiveDate = effectiveDate;
        PreviousAmount = previousAmount;
        Amount = amount;
        PreviousDescription = NormalizeOptionalText(previousDescription);
        Description = NormalizeOptionalText(description);
        OccurredUtc = utcNow;
    }

    public Guid ContributionId { get; private set; }
    public Guid CollaboratorId { get; private set; }
    public CollaboratorContributionEventType EventType { get; private set; }
    public DateOnly? PreviousEffectiveDate { get; private set; }
    public DateOnly EffectiveDate { get; private set; }
    public Money? PreviousAmount { get; private set; }
    public Money Amount { get; private set; }
    public string? PreviousDescription { get; private set; }
    public string? Description { get; private set; }
    public DateTime OccurredUtc { get; private set; }

    public static CollaboratorContributionEvent Created(
        CollaboratorContribution contribution,
        DateTime utcNow) => Create(
            contribution, CollaboratorContributionEventType.Created, null,
            contribution.Date, null, contribution.Amount, null, contribution.Description, utcNow);

    public static CollaboratorContributionEvent Edited(
        CollaboratorContribution contribution,
        DateOnly previousDate,
        Money previousAmount,
        string? previousDescription,
        DateTime utcNow) => Create(
            contribution, CollaboratorContributionEventType.Edited, previousDate,
            contribution.Date, previousAmount, contribution.Amount,
            previousDescription, contribution.Description, utcNow);

    public static CollaboratorContributionEvent Deleted(
        CollaboratorContribution contribution,
        DateTime utcNow) => Create(
            contribution, CollaboratorContributionEventType.Deleted, contribution.Date,
            contribution.Date, contribution.Amount, contribution.Amount,
            contribution.Description, contribution.Description, utcNow);

    private static CollaboratorContributionEvent Create(
        CollaboratorContribution contribution,
        CollaboratorContributionEventType eventType,
        DateOnly? previousEffectiveDate,
        DateOnly effectiveDate,
        Money? previousAmount,
        Money amount,
        string? previousDescription,
        string? description,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        return new CollaboratorContributionEvent(
            Guid.NewGuid(), contribution.Id, contribution.CollaboratorId, eventType,
            previousEffectiveDate, effectiveDate, previousAmount, amount,
            previousDescription, description, utcNow);
    }
}
