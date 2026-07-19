using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Obligations;

public sealed class ObligationPayment : AuditableEntity
{
    private ObligationPayment()
    {
    }

    private ObligationPayment(
        Guid id,
        Guid obligationId,
        DateOnly date,
        Money amount,
        DateTime utcNow) : base(id, utcNow)
    {
        ObligationId = obligationId;
        Date = date;
        Amount = amount.MinorUnits > 0
            ? amount
            : throw new ArgumentOutOfRangeException(nameof(amount), "El pago debe ser mayor que cero.");
    }

    public Guid ObligationId { get; private set; }

    public DateOnly Date { get; private set; }

    public Money Amount { get; private set; }

    public static ObligationPayment Create(
        Guid obligationId,
        DateOnly date,
        Money amount,
        DateTime utcNow) => new(Guid.NewGuid(), obligationId, date, amount, utcNow);

    public void Update(DateOnly date, Money amount, DateTime utcNow)
    {
        if (amount.MinorUnits == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount));
        }

        Date = date;
        Amount = amount;
        MarkUpdated(utcNow);
    }
}
