using PeluqueriaAdmin.Domain.Common;

namespace PeluqueriaAdmin.Domain.LocalUse;

public sealed class Chair : AuditableEntity
{
    private Chair()
    {
    }

    private Chair(Guid id, string name, DateOnly creationDate, string? description, DateTime utcNow)
        : base(id, utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        CreationDate = creationDate;
        Description = NormalizeOptionalText(description);
    }

    public string Name { get; private set; } = string.Empty;

    public DateOnly CreationDate { get; private set; }

    public string? Description { get; private set; }

    public Guid? AssignedPersonId { get; private set; }

    public static Chair Create(string name, DateOnly creationDate, string? description, DateTime utcNow) =>
        new(Guid.NewGuid(), name, creationDate, description, utcNow);

    public void Update(string name, DateOnly creationDate, string? description, DateTime utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        CreationDate = creationDate;
        Description = NormalizeOptionalText(description);
        MarkUpdated(utcNow);
    }

    public void Assign(Guid personId, DateTime utcNow)
    {
        if (personId == Guid.Empty)
        {
            throw new ArgumentException("El trabajador es obligatorio.", nameof(personId));
        }

        AssignedPersonId = personId;
        MarkUpdated(utcNow);
    }

    public void Unassign(DateTime utcNow)
    {
        AssignedPersonId = null;
        MarkUpdated(utcNow);
    }
}
