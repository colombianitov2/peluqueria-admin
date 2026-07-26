using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Collaborators;

public sealed class DistributionPayment : AuditableEntity
{
    private DistributionPayment()
    {
    }

    private DistributionPayment(
        Guid id,
        Guid participantId,
        DateOnly date,
        Money amount,
        DateTime utcNow,
        string? description = null) : base(id, utcNow)
    {
        ParticipantId = participantId;
        Date = date;
        Amount = amount;
        Description = NormalizeOptionalText(description);
    }

    public Guid ParticipantId { get; private set; }

    public DateOnly Date { get; private set; }

    public Money Amount { get; private set; }

    public string? Description { get; private set; }

    public static DistributionPayment Create(
        Guid participantId,
        DateOnly date,
        Money amount,
        Money pending,
        DateTime utcNow,
        string? description = null)
    {
        if (amount.MinorUnits <= 0 || amount.MinorUnits != pending.MinorUnits)
        {
            throw new InvalidOperationException("El pago debe cubrir el valor completo pendiente del cierre mensual.");
        }

        return new DistributionPayment(Guid.NewGuid(), participantId, date, amount, utcNow, description);
    }

    public void Update(DateOnly date, Money amount, Money available, DateTime utcNow, string? description = null)
    {
        if (amount.MinorUnits <= 0 || amount.MinorUnits != available.MinorUnits)
        {
            throw new InvalidOperationException("El pago editado debe conservar el valor completo de la asignación.");
        }

        Date = date;
        Amount = amount;
        Description = NormalizeOptionalText(description);
        MarkUpdated(utcNow);
    }
}
