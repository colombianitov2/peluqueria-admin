using PeluqueriaAdmin.Domain.Common;

namespace PeluqueriaAdmin.Domain.Activity;

public sealed class ActivityRecord : AuditableEntity
{
    private ActivityRecord()
    {
    }

    private ActivityRecord(
        Guid id,
        DateOnly activityDate,
        string module,
        string action,
        string summary,
        Guid? entityId,
        string? description,
        DateTime utcNow) : base(id, utcNow)
    {
        ActivityDate = activityDate;
        Module = NormalizeRequiredText(module, nameof(module));
        Action = NormalizeRequiredText(action, nameof(action));
        Summary = NormalizeRequiredText(summary, nameof(summary));
        EntityId = entityId;
        Description = NormalizeOptionalText(description);
        OccurredUtc = utcNow;
    }

    public DateOnly ActivityDate { get; private set; }

    public DateTime OccurredUtc { get; private set; }

    public string Module { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string Summary { get; private set; } = string.Empty;

    public Guid? EntityId { get; private set; }

    public string? Description { get; private set; }

    public static ActivityRecord Create(
        DateOnly activityDate,
        string module,
        string action,
        string summary,
        Guid? entityId,
        string? description,
        DateTime utcNow) => new(
            Guid.NewGuid(), activityDate, module, action, summary, entityId, description, utcNow);
}
