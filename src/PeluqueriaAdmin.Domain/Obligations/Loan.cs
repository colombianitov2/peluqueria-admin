using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Obligations;

public enum LoanFrequency { Weekly, Biweekly, Monthly }

public enum LoanCalculationMethod
{
    Legacy = 0,
    MonthlyBalanceInterest = 1,
    AgreedFinalAmount = 2,
}

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
        CalculationMethod = LoanCalculationMethod.Legacy;
        ExpectedTotal = initialBalance;
        TotalInterest = Money.FromMinorUnits(0);
        StartDate = startDate;
        Frequency = frequency;
        if (installmentCount is <= 0) throw new ArgumentOutOfRangeException(nameof(installmentCount));
        InstallmentCount = installmentCount;
        NextDueDate = nextDueDate;
        Description = NormalizeOptionalText(description);
    }

    internal Loan(
        Guid id,
        string name,
        Money principal,
        Money expectedTotal,
        Money totalInterest,
        Money usualInstallment,
        LoanCalculationMethod calculationMethod,
        int monthlyInterestBasisPoints,
        int equivalentMonthlyRateBasisPoints,
        int installmentCount,
        DateOnly firstDueDate,
        DateTime utcNow,
        string? description) : base(id, utcNow)
    {
        Name = NormalizeRequiredText(name, nameof(name));
        InitialBalance = EnsurePositive(principal, nameof(principal));
        ExpectedTotal = EnsurePositive(expectedTotal, nameof(expectedTotal));
        if (expectedTotal.MinorUnits < principal.MinorUnits)
            throw new ArgumentOutOfRangeException(nameof(expectedTotal), "El total esperado no puede ser inferior al principal.");
        TotalInterest = totalInterest;
        PendingBalance = expectedTotal;
        UsualInstallment = EnsurePositive(usualInstallment, nameof(usualInstallment));
        CalculationMethod = calculationMethod;
        MonthlyInterestBasisPoints = monthlyInterestBasisPoints;
        EquivalentMonthlyRateBasisPoints = equivalentMonthlyRateBasisPoints;
        StartDate = firstDueDate;
        Frequency = LoanFrequency.Monthly;
        InstallmentCount = installmentCount;
        NextDueDate = firstDueDate;
        Description = NormalizeOptionalText(description);
    }

    public string Name { get; private set; } = string.Empty;
    public Money InitialBalance { get; private set; }
    public Money PendingBalance { get; private set; }
    public Money UsualInstallment { get; private set; }
    public Money ExpectedTotal { get; private set; }
    public Money TotalInterest { get; private set; }
    public LoanCalculationMethod CalculationMethod { get; private set; }
    public int MonthlyInterestBasisPoints { get; private set; }
    public int EquivalentMonthlyRateBasisPoints { get; private set; }
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

    public void ApplyScheduledPayment(Money amount, DateOnly? nextDueDate, DateTime utcNow)
    {
        if (CalculationMethod == LoanCalculationMethod.Legacy)
        {
            ApplyPayment(amount, utcNow);
            return;
        }
        if (amount.MinorUnits <= 0 || amount.MinorUnits > PendingBalance.MinorUnits)
            throw new ArgumentOutOfRangeException(nameof(amount), "El pago debe corresponder a una cuota pendiente.");
        PendingBalance = Money.FromMinorUnits(PendingBalance.MinorUnits - amount.MinorUnits);
        if (!IsPaid && !nextDueDate.HasValue)
            throw new ArgumentException("Un préstamo pendiente debe conservar un próximo vencimiento.", nameof(nextDueDate));
        if (nextDueDate.HasValue) NextDueDate = nextDueDate.Value;
        MarkUpdated(utcNow);
    }

    private static Money EnsurePositive(Money amount, string name) => amount.MinorUnits > 0
        ? amount : throw new ArgumentOutOfRangeException(name, "El importe debe ser mayor que cero.");
}

public sealed class LoanPayment : AuditableEntity
{
    private LoanPayment() { }
    private LoanPayment(Guid id, Guid loanId, Guid? installmentId, DateOnly date, Money amount, DateTime utcNow,
        string? description) : base(id, utcNow)
    {
        LoanId = loanId;
        InstallmentId = installmentId;
        Date = date;
        Amount = amount.MinorUnits > 0 ? amount : throw new ArgumentOutOfRangeException(nameof(amount));
        Description = NormalizeOptionalText(description);
    }

    public Guid LoanId { get; private set; }
    public Guid? InstallmentId { get; private set; }
    public DateOnly Date { get; private set; }
    public Money Amount { get; private set; }
    public string? Description { get; private set; }

    public static LoanPayment Create(Guid loanId, DateOnly date, Money amount, DateTime utcNow,
        string? description = null) => new(Guid.NewGuid(), loanId, null, date, amount, utcNow, description);

    public static LoanPayment CreateScheduled(Guid loanId, Guid installmentId, DateOnly date,
        Money amount, DateTime utcNow, string? description = null)
    {
        if (installmentId == Guid.Empty) throw new ArgumentException("La cuota es obligatoria.", nameof(installmentId));
        return new LoanPayment(Guid.NewGuid(), loanId, installmentId, date, amount, utcNow, description);
    }
}
