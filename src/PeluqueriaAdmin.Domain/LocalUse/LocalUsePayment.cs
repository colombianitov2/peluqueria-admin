using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.LocalUse;

public sealed class LocalUsePayment : AuditableEntity
{
    private LocalUsePayment()
    {
    }

    private LocalUsePayment(
        Guid id,
        Guid personId,
        DateOnly paymentDate,
        Money amount,
        DateTime utcNow,
        string? description = null) : base(id, utcNow)
    {
        PersonId = personId;
        PaymentDate = paymentDate;
        Amount = amount;
        Description = NormalizeOptionalText(description);
    }

    public Guid PersonId { get; private set; }

    public DateOnly PaymentDate { get; private set; }

    public Money Amount { get; private set; }

    public string? Description { get; private set; }

    public static LocalUsePayment Create(
        Guid personId,
        DateOnly paymentDate,
        Money amount,
        DateTime utcNow,
        string? description = null)
    {
        if (amount.MinorUnits == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "El pago debe ser mayor que cero.");
        }

        return new LocalUsePayment(Guid.NewGuid(), personId, paymentDate, amount, utcNow, description);
    }

    public void Update(
        DateOnly paymentDate,
        Money amount,
        Money debtBeforeThisPayment,
        DateTime utcNow,
        string? description = null)
    {
        _ = debtBeforeThisPayment;
        if (amount.MinorUnits == 0)
        {
            throw new InvalidOperationException("El pago editado debe ser mayor que cero.");
        }

        PaymentDate = paymentDate;
        Amount = amount;
        Description = NormalizeOptionalText(description);
        MarkUpdated(utcNow);
    }
}
