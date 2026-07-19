using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Collaborators;

public sealed class CollaboratorContribution : AuditableEntity
{
    private CollaboratorContribution()
    {
    }

    private CollaboratorContribution(
        Guid id,
        Guid collaboratorId,
        DateOnly date,
        Money amount,
        string? description,
        DateTime utcNow) : base(id, utcNow)
    {
        if (collaboratorId == Guid.Empty)
        {
            throw new ArgumentException("El colaborador es obligatorio.", nameof(collaboratorId));
        }

        if (amount.MinorUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "El aporte debe ser mayor que cero.");
        }

        CollaboratorId = collaboratorId;
        Date = date;
        Amount = amount;
        Description = NormalizeOptionalText(description);
    }

    public Guid CollaboratorId { get; private set; }

    public DateOnly Date { get; private set; }

    public Money Amount { get; private set; }

    public string? Description { get; private set; }

    public static CollaboratorContribution Create(
        Guid collaboratorId,
        DateOnly date,
        Money amount,
        string? description,
        DateTime utcNow) => new(Guid.NewGuid(), collaboratorId, date, amount, description, utcNow);

    public void Update(DateOnly date, Money amount, string? description, DateTime utcNow)
    {
        if (amount.MinorUnits <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "El aporte debe ser mayor que cero.");
        }

        Date = date;
        Amount = amount;
        Description = NormalizeOptionalText(description);
        MarkUpdated(utcNow);
    }
}
