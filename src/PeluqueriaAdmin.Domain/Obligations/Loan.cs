using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Obligations;

public enum LoanFrequency { Weekly, Biweekly, Monthly }

public sealed class Loan : AuditableEntity
{
    private Loan() { }

    private Loan(Guid id, string name, Money initialBalance, Money usualInstallment,
        DateOnly startDate, LoanFrequency frequency, int? installmentCount, DateOnly nextDueDate,
        DateTime utcNow, string? description) : base(id, utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        InitialBalance = EnsurePositive(initialBalance, nameof(initialBalance));
        PendingBalance = initialBalance;
        UsualInstallment = EnsurePositive(usualInstallment, nameof(usualInstallment));
        StartDate = startDate;
        Frequency = frequency;
        if (installmentCount is <= 0) throw new ArgumentOutOfRangeException(nameof(installmentCount));
        InstallmentCount = installmentCount;
        NextDueDate = nextDueDate;
        Description = NormalizeOptionalText(description);
    }

    public string Name { get; private set; } = string.Empty;
    public Money InitialBalance { get; private set; }
    public Money PendingBalance { get; private set; }
    public Money UsualInstallment { get; private set; }
    public DateOnly StartDate { get; private set; }
    public LoanFrequency Frequency { get; private set; }
    public int? InstallmentCount { get; private set; }
    public DateOnly NextDueDate { get; private set; }
    public string? Description { get; private set; }
    public bool IsPaid => PendingBalance.MinorUnits == 0;

    public static Loan Create(string name, Money initialBalance, Money usualInstallment,
        DateOnly startDate, LoanFrequency frequency, int? installmentCount, DateOnly nextDueDate,
        DateTime utcNow, string? description = null) => new(Guid.NewGuid(), name, initialBalance,
            usualInstallment, startDate, frequency, installmentCount, nextDueDate, utcNow, description);

    public void ApplyPayment(Money amount, DateTime utcNow)
    {
        if (amount.MinorUnits <= 0 || amount.MinorUnits > PendingBalance.MinorUnits)
            throw new ArgumentOutOfRangeException(nameof(amount), "El pago debe ser positivo y no superar el saldo pendiente.");
        PendingBalance = Money.FromMinorUnits(PendingBalance.MinorUnits - amount.MinorUnits);
        if (!IsPaid) NextDueDate = Frequency switch
        {
            LoanFrequency.Weekly => NextDueDate.AddDays(7),
            LoanFrequency.Biweekly => NextDueDate.AddDays(15),
            _ => NextDueDate.AddMonths(1),
        };
        MarkUpdated(utcNow);
    }

    private static Money EnsurePositive(Money amount, string name) => amount.MinorUnits > 0
        ? amount : throw new ArgumentOutOfRangeException(name, "El importe debe ser mayor que cero.");
}

public sealed class LoanPayment : AuditableEntity
{
    private LoanPayment() { }
    private LoanPayment(Guid id, Guid loanId, DateOnly date, Money amount, DateTime utcNow,
        string? description) : base(id, utcNow)
    {
        LoanId = loanId;
        Date = date;
        Amount = amount.MinorUnits > 0 ? amount : throw new ArgumentOutOfRangeException(nameof(amount));
        Description = NormalizeOptionalText(description);
    }

    public Guid LoanId { get; private set; }
    public DateOnly Date { get; private set; }
    public Money Amount { get; private set; }
    public string? Description { get; private set; }

    public static LoanPayment Create(Guid loanId, DateOnly date, Money amount, DateTime utcNow,
        string? description = null) => new(Guid.NewGuid(), loanId, date, amount, utcNow, description);
}
