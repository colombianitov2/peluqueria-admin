using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Collaborators;

public sealed class Collaborator : AuditableEntity
{
    private Collaborator()
    {
    }

    private Collaborator(
        Guid id,
        string name,
        DateOnly startDate,
        DateOnly? exitDate,
        DateTime utcNow,
        string? description = null) : base(id, utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        StartDate = startDate;
        ValidateExit(startDate, exitDate);
        ExitDate = exitDate;
        Description = NormalizeOptionalText(description);
    }

    public string Name { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly? ExitDate { get; private set; }

    public string? Description { get; private set; }

    public int ProfitShareBasisPoints { get; private set; }

    public static Collaborator Create(
        string name,
        DateOnly startDate,
        DateOnly? exitDate,
        DateTime utcNow,
        string? description = null) => new(Guid.NewGuid(), name, startDate, exitDate, utcNow, description);

    public bool IsCurrentOn(DateOnly date) =>
        !IsDeleted && StartDate <= date && (!ExitDate.HasValue || date < ExitDate.Value);

    public void Update(
        string name,
        DateOnly startDate,
        DateOnly? exitDate,
        DateTime utcNow,
        string? description = null)
    {
        ValidateExit(startDate, exitDate);
        Name = NormalizeRequiredText(name, nameof(name));
        StartDate = startDate;
        ExitDate = exitDate;
        Description = NormalizeOptionalText(description);
        MarkUpdated(utcNow);
    }

    public void UpdateProfitShare(Percentage share, DateTime utcNow)
    {
        ProfitShareBasisPoints = share.BasisPoints;
        MarkUpdated(utcNow);
    }

    private static void ValidateExit(DateOnly startDate, DateOnly? exitDate)
    {
        if (exitDate.HasValue && exitDate.Value < startDate)
        {
            throw new ArgumentException("La fecha de retiro no puede ser anterior al inicio.", nameof(exitDate));
        }
    }
}
