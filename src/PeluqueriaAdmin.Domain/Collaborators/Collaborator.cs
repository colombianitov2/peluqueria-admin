using PeluqueriaAdmin.Domain.Common;

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
        DateTime utcNow) : base(id, utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        StartDate = startDate;
        ValidateExit(startDate, exitDate);
        ExitDate = exitDate;
    }

    public string Name { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly? ExitDate { get; private set; }

    public static Collaborator Create(
        string name,
        DateOnly startDate,
        DateOnly? exitDate,
        DateTime utcNow) => new(Guid.NewGuid(), name, startDate, exitDate, utcNow);

    public bool IsCurrentOn(DateOnly date) =>
        !IsDeleted && StartDate <= date && (!ExitDate.HasValue || ExitDate.Value >= date);

    public void Update(string name, DateOnly startDate, DateOnly? exitDate, DateTime utcNow)
    {
        ValidateExit(startDate, exitDate);
        Name = NormalizeRequiredText(name, nameof(name));
        StartDate = startDate;
        ExitDate = exitDate;
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
