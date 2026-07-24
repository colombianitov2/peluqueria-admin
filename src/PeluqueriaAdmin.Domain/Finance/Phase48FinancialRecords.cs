using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Finance;

public enum FinancialCommitmentSource
{
    Obligation,
    Maintenance,
    MonthlyPurchase,
    LoanInstallment,
    PriorUncovered,
}

public sealed class FinancialReserve : AuditableEntity
{
    private FinancialReserve() { }

    private FinancialReserve(Guid id, YearMonth month, FinancialCommitmentSource sourceType,
        Guid sourceId, string name, DateOnly dueDate, Money reservedAmount, DateTime utcNow) : base(id, utcNow)
    {
        Month = month;
        SourceType = sourceType;
        SourceId = sourceId;
        Name = NormalizeRequiredText(name, nameof(name));
        DueDate = dueDate;
        ReservedAmount = EnsurePositive(reservedAmount);
    }

    public YearMonth Month { get; private set; }
    public FinancialCommitmentSource SourceType { get; private set; }
    public Guid SourceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly DueDate { get; private set; }
    public Money ReservedAmount { get; private set; }
    public Money? ActualAmount { get; private set; }
    public DateOnly? SettledDate { get; private set; }
    public bool IsConsumed => SettledDate.HasValue;

    public static FinancialReserve Create(YearMonth month, FinancialCommitmentSource sourceType,
        Guid sourceId, string name, DateOnly dueDate, Money amount, DateTime utcNow) =>
        new(Guid.NewGuid(), month, sourceType, sourceId, name, dueDate, amount, utcNow);

    public void Settle(DateOnly date, Money actualAmount, DateTime utcNow)
    {
        if (IsConsumed) throw new InvalidOperationException("La reserva ya fue consumida.");
        if (actualAmount.MinorUnits < 0) throw new ArgumentOutOfRangeException(nameof(actualAmount));
        SettledDate = date;
        ActualAmount = actualAmount;
        MarkUpdated(utcNow);
    }

    private static Money EnsurePositive(Money amount) => amount.MinorUnits > 0
        ? amount
        : throw new ArgumentOutOfRangeException(nameof(amount), "La reserva debe ser mayor que cero.");
}

public sealed class FinancialCloseExclusion : AuditableEntity
{
    private FinancialCloseExclusion() { }

    private FinancialCloseExclusion(Guid id, YearMonth month, FinancialCommitmentSource sourceType,
        Guid sourceId, string reason, DateTime utcNow) : base(id, utcNow)
    {
        Month = month;
        SourceType = sourceType;
        SourceId = sourceId;
        Reason = NormalizeRequiredText(reason, nameof(reason));
    }

    public YearMonth Month { get; private set; }
    public FinancialCommitmentSource SourceType { get; private set; }
    public Guid SourceId { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    public static FinancialCloseExclusion Create(YearMonth month, FinancialCommitmentSource sourceType,
        Guid sourceId, string reason, DateTime utcNow) =>
        new(Guid.NewGuid(), month, sourceType, sourceId, reason, utcNow);

    public void UpdateReason(string reason, DateTime utcNow)
    {
        Reason = NormalizeRequiredText(reason, nameof(reason));
        MarkUpdated(utcNow);
    }
}

public sealed class AnnualClose : AuditableEntity
{
    private AnnualClose() { }

    private AnnualClose(Guid id, int year, long income, long paidOutflows, long reserves,
        long obligations, long loanPayments, long collaboratorFund, long result, DateTime utcNow)
        : this(id, year, income, paidOutflows, reserves, obligations, loanPayments,
            collaboratorFund, result, 0, 0, reserves, 0, Math.Max(result, 0),
            Math.Max(-result, 0), result, result, utcNow)
    {
    }

    private AnnualClose(Guid id, int year, long income, long paidOutflows, long reserves,
        long obligations, long loanPayments, long collaboratorFund, long result,
        long accountsReceivable, long accountsPayable, long pendingReserves, long pendingLoans,
        long surplus, long deficit, long availableBalance, long projectedNextYearBalance,
        DateTime utcNow) : base(id, utcNow)
    {
        if (year is < 2000 or > 2200) throw new ArgumentOutOfRangeException(nameof(year));
        Year = year;
        IncomeMinorUnits = income;
        PaidOutflowsMinorUnits = paidOutflows;
        ReservesMinorUnits = reserves;
        ObligationsMinorUnits = obligations;
        LoanPaymentsMinorUnits = loanPayments;
        CollaboratorFundMinorUnits = collaboratorFund;
        ResultMinorUnits = result;
        AccountsReceivableMinorUnits = accountsReceivable;
        AccountsPayableMinorUnits = accountsPayable;
        PendingReservesMinorUnits = pendingReserves;
        PendingLoansMinorUnits = pendingLoans;
        SurplusMinorUnits = surplus;
        DeficitMinorUnits = deficit;
        AvailableBalanceMinorUnits = availableBalance;
        ProjectedNextYearBalanceMinorUnits = projectedNextYearBalance;
        ClosedUtc = utcNow;
    }

    public int Year { get; private set; }
    public long IncomeMinorUnits { get; private set; }
    public long PaidOutflowsMinorUnits { get; private set; }
    public long ReservesMinorUnits { get; private set; }
    public long ObligationsMinorUnits { get; private set; }
    public long LoanPaymentsMinorUnits { get; private set; }
    public long CollaboratorFundMinorUnits { get; private set; }
    public long ResultMinorUnits { get; private set; }
    public long AccountsReceivableMinorUnits { get; private set; }
    public long AccountsPayableMinorUnits { get; private set; }
    public long PendingReservesMinorUnits { get; private set; }
    public long PendingLoansMinorUnits { get; private set; }
    public long SurplusMinorUnits { get; private set; }
    public long DeficitMinorUnits { get; private set; }
    public long AvailableBalanceMinorUnits { get; private set; }
    public long ProjectedNextYearBalanceMinorUnits { get; private set; }
    public DateTime ClosedUtc { get; private set; }

    public static AnnualClose Create(int year, long income, long paidOutflows, long reserves,
        long obligations, long loanPayments, long collaboratorFund, long result, DateTime utcNow) =>
        new(Guid.NewGuid(), year, income, paidOutflows, reserves, obligations, loanPayments,
            collaboratorFund, result, utcNow);

    public static AnnualClose Create(
        int year,
        long income,
        long paidOutflows,
        long reserves,
        long obligations,
        long loanPayments,
        long collaboratorFund,
        long result,
        long accountsReceivable,
        long accountsPayable,
        long pendingReserves,
        long pendingLoans,
        long surplus,
        long deficit,
        long availableBalance,
        long projectedNextYearBalance,
        DateTime utcNow) => new(
            Guid.NewGuid(), year, income, paidOutflows, reserves, obligations, loanPayments,
            collaboratorFund, result, accountsReceivable, accountsPayable, pendingReserves,
            pendingLoans, surplus, deficit, availableBalance, projectedNextYearBalance, utcNow);
}

public sealed class AnnualCarryover : AuditableEntity
{
    private AnnualCarryover()
    {
    }

    private AnnualCarryover(
        Guid id,
        int sourceYear,
        long accountsReceivable,
        long accountsPayable,
        long pendingReserves,
        long pendingLoans,
        long surplus,
        long deficit,
        DateTime utcNow) : base(id, utcNow)
    {
        if (sourceYear is < 2000 or > 2200) throw new ArgumentOutOfRangeException(nameof(sourceYear));
        SourceYear = sourceYear;
        TargetYear = checked(sourceYear + 1);
        AccountsReceivableMinorUnits = EnsureNonNegative(accountsReceivable, nameof(accountsReceivable));
        AccountsPayableMinorUnits = EnsureNonNegative(accountsPayable, nameof(accountsPayable));
        PendingReservesMinorUnits = EnsureNonNegative(pendingReserves, nameof(pendingReserves));
        PendingLoansMinorUnits = EnsureNonNegative(pendingLoans, nameof(pendingLoans));
        SurplusMinorUnits = EnsureNonNegative(surplus, nameof(surplus));
        DeficitMinorUnits = EnsureNonNegative(deficit, nameof(deficit));
    }

    public int SourceYear { get; private set; }
    public int TargetYear { get; private set; }
    public long AccountsReceivableMinorUnits { get; private set; }
    public long AccountsPayableMinorUnits { get; private set; }
    public long PendingReservesMinorUnits { get; private set; }
    public long PendingLoansMinorUnits { get; private set; }
    public long SurplusMinorUnits { get; private set; }
    public long DeficitMinorUnits { get; private set; }

    public static AnnualCarryover Create(
        int sourceYear,
        long accountsReceivable,
        long accountsPayable,
        long pendingReserves,
        long pendingLoans,
        long surplus,
        long deficit,
        DateTime utcNow) => new(
            Guid.NewGuid(), sourceYear, accountsReceivable, accountsPayable, pendingReserves,
            pendingLoans, surplus, deficit, utcNow);

    private static long EnsureNonNegative(long value, string name) =>
        value >= 0 ? value : throw new ArgumentOutOfRangeException(name);
}
