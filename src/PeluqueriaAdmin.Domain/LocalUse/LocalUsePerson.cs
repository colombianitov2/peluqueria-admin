using PeluqueriaAdmin.Domain.Common;

namespace PeluqueriaAdmin.Domain.LocalUse;

public sealed class LocalUsePerson : AuditableEntity
{
    private LocalUsePerson()
    {
    }

    private LocalUsePerson(
        Guid id,
        string name,
        DateOnly entryDate,
        DateOnly? exitDate,
        DateTime utcNow,
        string? description = null) : base(id, utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        EntryDate = entryDate;
        ValidateExitDate(entryDate, exitDate);
        ExitDate = exitDate;
        Description = NormalizeOptionalText(description);
    }

    public string Name { get; private set; } = string.Empty;

    public DateOnly EntryDate { get; private set; }

    public DateOnly? ExitDate { get; private set; }

    public string? Description { get; private set; }

    public static LocalUsePerson Create(
        string name,
        DateOnly entryDate,
        DateOnly? exitDate,
        DateTime utcNow,
        string? description = null) => new(Guid.NewGuid(), name, entryDate, exitDate, utcNow, description);

    public bool IsCurrentOn(DateOnly date) =>
        !IsDeleted && EntryDate <= date && (!ExitDate.HasValue || ExitDate.Value >= date);

    public void Update(
        string name,
        DateOnly entryDate,
        DateOnly? exitDate,
        DateTime utcNow,
        string? description = null)
    {
        ValidateExitDate(entryDate, exitDate);
        Name = NormalizeRequiredText(name, nameof(name));
        EntryDate = entryDate;
        ExitDate = exitDate;
        Description = NormalizeOptionalText(description);
        MarkUpdated(utcNow);
    }

    private static void ValidateExitDate(DateOnly entryDate, DateOnly? exitDate)
    {
        if (exitDate.HasValue && exitDate.Value < entryDate)
        {
            throw new ArgumentException("La fecha de retiro no puede ser anterior a la fecha de ingreso.", nameof(exitDate));
        }
    }
}
