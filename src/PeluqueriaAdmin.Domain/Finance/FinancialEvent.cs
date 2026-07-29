using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Finance;

public sealed class FinancialEvent : AuditableEntity
{
    private FinancialEvent()
    {
    }

    private FinancialEvent(
        Guid id,
        Guid operationId,
        DateTime occurredUtc,
        string entityType,
        Guid entityId,
        string eventType,
        Money? previousValue,
        Money? newValue,
        long differenceMinorUnits,
        string? description,
        string state) : base(id, occurredUtc)
    {
        if (operationId == Guid.Empty) throw new ArgumentException("La operación es obligatoria.", nameof(operationId));
        if (entityId == Guid.Empty) throw new ArgumentException("La entidad es obligatoria.", nameof(entityId));
        OperationId = operationId;
        OccurredUtc = occurredUtc;
        EntityType = NormalizeRequiredText(entityType, nameof(entityType));
        EntityId = entityId;
        EventType = NormalizeRequiredText(eventType, nameof(eventType));
        PreviousValue = previousValue;
        NewValue = newValue;
        DifferenceMinorUnits = differenceMinorUnits;
        Description = NormalizeOptionalText(description);
        State = NormalizeRequiredText(state, nameof(state));
    }

    public Guid OperationId { get; private set; }
    public DateTime OccurredUtc { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public Money? PreviousValue { get; private set; }
    public Money? NewValue { get; private set; }
    public long DifferenceMinorUnits { get; private set; }
    public string? Description { get; private set; }
    public string State { get; private set; } = string.Empty;

    public static FinancialEvent Create(
        Guid operationId,
        DateTime occurredUtc,
        string entityType,
        Guid entityId,
        string eventType,
        Money? previousValue,
        Money? newValue,
        long differenceMinorUnits,
        string? description,
        string state = "Registrado") =>
        new(
            Guid.NewGuid(),
            operationId,
            occurredUtc,
            entityType,
            entityId,
            eventType,
            previousValue,
            newValue,
            differenceMinorUnits,
            description,
            state);
}
