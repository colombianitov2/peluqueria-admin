using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Maintenance;

public sealed class MaintenanceRecord : AuditableEntity
{
    private MaintenanceRecord()
    {
    }

    private MaintenanceRecord(
        Guid id,
        string asset,
        string maintenanceType,
        DateOnly scheduledDate,
        Money? estimatedCost,
        DateOnly? completedDate,
        Money? actualCost,
        DateTime utcNow,
        string? description = null) : base(id, utcNow)
    {
        Asset = NormalizeRequiredText(asset, nameof(asset));
        MaintenanceType = NormalizeRequiredText(maintenanceType, nameof(maintenanceType));
        ScheduledDate = scheduledDate;
        EstimatedCost = estimatedCost;
        CompletedDate = completedDate;
        ActualCost = actualCost;
        ValidateCompletion(completedDate, actualCost);
        Description = NormalizeOptionalText(description);
    }

    public string Asset { get; private set; } = string.Empty;

    public string MaintenanceType { get; private set; } = string.Empty;

    public DateOnly ScheduledDate { get; private set; }

    public Money? EstimatedCost { get; private set; }

    public DateOnly? CompletedDate { get; private set; }

    public Money? ActualCost { get; private set; }

    public string? Description { get; private set; }

    public static MaintenanceRecord Create(
        string asset,
        string maintenanceType,
        DateOnly scheduledDate,
        Money? estimatedCost,
        DateOnly? completedDate,
        Money? actualCost,
        DateTime utcNow,
        string? description = null) => new(
            Guid.NewGuid(), asset, maintenanceType, scheduledDate,
            estimatedCost, completedDate, actualCost, utcNow, description);

    public void Update(
        string asset,
        string maintenanceType,
        DateOnly scheduledDate,
        Money? estimatedCost,
        DateOnly? completedDate,
        Money? actualCost,
        DateTime utcNow,
        string? description = null)
    {
        ValidateCompletion(completedDate, actualCost);
        Asset = NormalizeRequiredText(asset, nameof(asset));
        MaintenanceType = NormalizeRequiredText(maintenanceType, nameof(maintenanceType));
        ScheduledDate = scheduledDate;
        EstimatedCost = estimatedCost;
        CompletedDate = completedDate;
        ActualCost = actualCost;
        Description = NormalizeOptionalText(description);
        MarkUpdated(utcNow);
    }

    public bool NeedsAttention(DateOnly today) => !IsDeleted && !CompletedDate.HasValue && ScheduledDate <= today;

    public Money GoalAmountFor(YearMonth month)
    {
        if (CompletedDate.HasValue && YearMonth.From(CompletedDate.Value) == month)
        {
            return ActualCost ?? EstimatedCost ?? Money.FromMinorUnits(0);
        }

        if (!CompletedDate.HasValue && YearMonth.From(ScheduledDate) == month)
        {
            return EstimatedCost ?? Money.FromMinorUnits(0);
        }

        return Money.FromMinorUnits(0);
    }

    private static void ValidateCompletion(DateOnly? completedDate, Money? actualCost)
    {
        if (actualCost.HasValue && !completedDate.HasValue)
        {
            throw new ArgumentException("Un costo real requiere fecha realizada.", nameof(actualCost));
        }
    }
}
