using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Collaborators;

public sealed class MonthlyCloseParticipant : AuditableEntity
{
    private MonthlyCloseParticipant()
    {
    }

    internal MonthlyCloseParticipant(
        Guid id,
        Guid closeId,
        Guid collaboratorId,
        Money amount,
        DateTime utcNow) : base(id, utcNow)
    {
        CloseId = closeId;
        CollaboratorId = collaboratorId;
        Amount = amount;
    }

    public Guid CloseId { get; private set; }

    public Guid CollaboratorId { get; private set; }

    public Money Amount { get; private set; }
}
