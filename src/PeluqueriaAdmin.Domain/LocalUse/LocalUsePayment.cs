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
        DateTime utcNow) : base(id, utcNow)
    {
        PersonId = personId;
        PaymentDate = paymentDate;
        Amount = amount;
    }

    public Guid PersonId { get; private set; }

    public DateOnly PaymentDate { get; private set; }

    public Money Amount { get; private set; }

    public static LocalUsePayment Create(
        Guid personId,
        DateOnly paymentDate,
        Money amount,
        Money currentDebt,
        DateTime utcNow)
    {
        if (amount.MinorUnits == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "El pago debe ser mayor que cero.");
        }

        if (amount.MinorUnits > currentDebt.MinorUnits)
        {
            throw new InvalidOperationException("El pago no puede superar la deuda actual.");
        }

        return new LocalUsePayment(Guid.NewGuid(), personId, paymentDate, amount, utcNow);
    }

    public void Update(
        DateOnly paymentDate,
        Money amount,
        Money debtBeforeThisPayment,
        DateTime utcNow)
    {
        if (amount.MinorUnits == 0 || amount.MinorUnits > debtBeforeThisPayment.MinorUnits)
        {
            throw new InvalidOperationException("El pago editado debe ser mayor que cero y no superar la deuda disponible.");
        }

        PaymentDate = paymentDate;
        Amount = amount;
        MarkUpdated(utcNow);
    }
}
