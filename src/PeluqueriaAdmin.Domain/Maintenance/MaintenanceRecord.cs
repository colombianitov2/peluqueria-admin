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
        string? description = null,
        MaintenanceFrequency frequency = MaintenanceFrequency.Once,
        int? customInterval = null,
        MaintenanceIntervalUnit? customIntervalUnit = null,
        Guid? seriesId = null,
        DateOnly? firstScheduledDate = null,
        int occurrenceNumber = 0) : base(id, utcNow)
    {
        Asset = NormalizeRequiredText(asset, nameof(asset));
        MaintenanceType = NormalizeRequiredText(maintenanceType, nameof(maintenanceType));
        ScheduledDate = scheduledDate;
        EstimatedCost = estimatedCost;
        CompletedDate = completedDate;
        ActualCost = actualCost;
        ValidateCompletion(completedDate, actualCost);
        Description = NormalizeOptionalText(description);
        ValidateRecurrence(frequency, customInterval, customIntervalUnit);
        Frequency = frequency;
        CustomInterval = customInterval;
        CustomIntervalUnit = customIntervalUnit;
        SeriesId = seriesId ?? Guid.NewGuid();
        FirstScheduledDate = firstScheduledDate ?? scheduledDate;
        OccurrenceNumber = occurrenceNumber;
    }

    public string Asset { get; private set; } = string.Empty;

    public string MaintenanceType { get; private set; } = string.Empty;

    public DateOnly ScheduledDate { get; private set; }

    public Money? EstimatedCost { get; private set; }

    public DateOnly? CompletedDate { get; private set; }

    public Money? ActualCost { get; private set; }

    public string? Description { get; private set; }

    public MaintenanceFrequency Frequency { get; private set; }

    public int? CustomInterval { get; private set; }

    public MaintenanceIntervalUnit? CustomIntervalUnit { get; private set; }

    public Guid SeriesId { get; private set; }

    public DateOnly FirstScheduledDate { get; private set; }

    public int OccurrenceNumber { get; private set; }

    public bool IsRecurring => Frequency != MaintenanceFrequency.Once;

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

    public static MaintenanceRecord Schedule(
        string asset,
        string maintenanceType,
        DateOnly scheduledDate,
        Money? estimatedCost,
        MaintenanceFrequency frequency,
        int? customInterval,
        MaintenanceIntervalUnit? customIntervalUnit,
        DateTime utcNow,
        string? description = null) => new(
            Guid.NewGuid(), asset, maintenanceType, scheduledDate,
            estimatedCost, null, null, utcNow, description,
            frequency, customInterval, customIntervalUnit);

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

    public void Complete(DateOnly completedDate, Money actualCost, DateTime utcNow, string? description = null)
    {
        if (CompletedDate.HasValue)
        {
            return;
        }
        CompletedDate = completedDate;
        ActualCost = actualCost;
        Description = NormalizeOptionalText(description) ?? Description;
        MarkUpdated(utcNow);
    }

    public MaintenanceRecord CreateNext(DateTime utcNow)
    {
        if (!IsRecurring)
        {
            throw new InvalidOperationException("El mantenimiento no es recurrente.");
        }
        int nextOccurrence = checked(OccurrenceNumber + 1);
        DateOnly nextDate = CalculateOccurrenceDate(
            FirstScheduledDate, Frequency, CustomInterval, CustomIntervalUnit, nextOccurrence);
        return new MaintenanceRecord(
            Guid.NewGuid(), Asset, MaintenanceType, nextDate, EstimatedCost, null, null, utcNow,
            Description, Frequency, CustomInterval, CustomIntervalUnit, SeriesId, FirstScheduledDate, nextOccurrence);
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

    public static DateOnly CalculateOccurrenceDate(
        DateOnly anchor,
        MaintenanceFrequency frequency,
        int? customInterval,
        MaintenanceIntervalUnit? customIntervalUnit,
        int occurrenceNumber)
    {
        if (occurrenceNumber < 0) throw new ArgumentOutOfRangeException(nameof(occurrenceNumber));
        return frequency switch
        {
            MaintenanceFrequency.Once => anchor,
            MaintenanceFrequency.Weekly => anchor.AddDays(7 * occurrenceNumber),
            MaintenanceFrequency.Biweekly => anchor.AddDays(15 * occurrenceNumber),
            MaintenanceFrequency.Monthly => anchor.AddMonths(occurrenceNumber),
            MaintenanceFrequency.EveryTwoMonths => anchor.AddMonths(2 * occurrenceNumber),
            MaintenanceFrequency.EveryThreeMonths => anchor.AddMonths(3 * occurrenceNumber),
            MaintenanceFrequency.EverySixMonths => anchor.AddMonths(6 * occurrenceNumber),
            MaintenanceFrequency.Yearly => anchor.AddYears(occurrenceNumber),
            MaintenanceFrequency.Custom => AddCustom(anchor, customInterval!.Value, customIntervalUnit!.Value, occurrenceNumber),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency)),
        };
    }

    private static DateOnly AddCustom(DateOnly anchor, int interval, MaintenanceIntervalUnit unit, int occurrenceNumber) => unit switch
    {
        MaintenanceIntervalUnit.Days => anchor.AddDays(checked(interval * occurrenceNumber)),
        MaintenanceIntervalUnit.Weeks => anchor.AddDays(checked(interval * 7 * occurrenceNumber)),
        MaintenanceIntervalUnit.Months => anchor.AddMonths(checked(interval * occurrenceNumber)),
        MaintenanceIntervalUnit.Years => anchor.AddYears(checked(interval * occurrenceNumber)),
        _ => throw new ArgumentOutOfRangeException(nameof(unit)),
    };

    private static void ValidateRecurrence(
        MaintenanceFrequency frequency,
        int? customInterval,
        MaintenanceIntervalUnit? customIntervalUnit)
    {
        if (frequency == MaintenanceFrequency.Custom)
        {
            if (!customInterval.HasValue || customInterval.Value <= 0 || !customIntervalUnit.HasValue)
            {
                throw new ArgumentException("La frecuencia personalizada requiere un intervalo positivo y una unidad.");
            }
        }
        else if (customInterval.HasValue || customIntervalUnit.HasValue)
        {
            throw new ArgumentException("El intervalo personalizado solo se usa con frecuencia personalizada.");
        }
    }
}
