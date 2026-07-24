using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Obligations;

public sealed class LoanInstallment : AuditableEntity
{
    private LoanInstallment()
    {
    }

    private LoanInstallment(
        Guid id,
        Guid loanId,
        int number,
        DateOnly dueDate,
        Money amount,
        Money principal,
        Money interest,
        Money principalBalanceAfter,
        string? description,
        DateTime utcNow) : base(id, utcNow)
    {
        if (loanId == Guid.Empty) throw new ArgumentException("El préstamo es obligatorio.", nameof(loanId));
        if (number <= 0) throw new ArgumentOutOfRangeException(nameof(number));
        if (amount.MinorUnits <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (principal.MinorUnits + interest.MinorUnits != amount.MinorUnits)
            throw new ArgumentException("Capital e interés deben sumar exactamente el valor de la cuota.");
        LoanId = loanId;
        Number = number;
        DueDate = dueDate;
        Amount = amount;
        Principal = principal;
        Interest = interest;
        PrincipalBalanceAfter = principalBalanceAfter;
        Description = NormalizeOptionalText(description);
    }

    public Guid LoanId { get; private set; }
    public int Number { get; private set; }
    public DateOnly DueDate { get; private set; }
    public Money Amount { get; private set; }
    public Money Principal { get; private set; }
    public Money Interest { get; private set; }
    public Money PrincipalBalanceAfter { get; private set; }
    public string? Description { get; private set; }

    internal static LoanInstallment Create(
        Guid loanId,
        int number,
        DateOnly dueDate,
        long amountMinorUnits,
        long principalMinorUnits,
        long interestMinorUnits,
        long principalBalanceAfterMinorUnits,
        string? description,
        DateTime utcNow) => new(
            Guid.NewGuid(), loanId, number, dueDate,
            Money.FromMinorUnits(amountMinorUnits),
            Money.FromMinorUnits(principalMinorUnits),
            Money.FromMinorUnits(interestMinorUnits),
            Money.FromMinorUnits(principalBalanceAfterMinorUnits),
            description,
            utcNow);
}
