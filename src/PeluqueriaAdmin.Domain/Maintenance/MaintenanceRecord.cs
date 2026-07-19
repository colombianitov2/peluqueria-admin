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
        DateTime utcNow) : base(id, utcNow)
    {
        Asset = NormalizeRequiredText(asset, nameof(asset));
        MaintenanceType = NormalizeRequiredText(maintenanceType, nameof(maintenanceType));
        ScheduledDate = scheduledDate;
        EstimatedCost = estimatedCost;
        CompletedDate = completedDate;
        ActualCost = actualCost;
        ValidateCompletion(completedDate, actualCost);
    }

    public string Asset { get; private set; } = string.Empty;

    public string MaintenanceType { get; private set; } = string.Empty;

    public DateOnly ScheduledDate { get; private set; }

    public Money? EstimatedCost { get; private set; }

    public DateOnly? CompletedDate { get; private set; }

    public Money? ActualCost { get; private set; }

    public static MaintenanceRecord Create(
        string asset,
        string maintenanceType,
        DateOnly scheduledDate,
        Money? estimatedCost,
        DateOnly? completedDate,
        Money? actualCost,
        DateTime utcNow) => new(
            Guid.NewGuid(), asset, maintenanceType, scheduledDate,
            estimatedCost, completedDate, actualCost, utcNow);

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
