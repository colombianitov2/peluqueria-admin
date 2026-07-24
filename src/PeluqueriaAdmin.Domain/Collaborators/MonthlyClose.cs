using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Collaborators;

public sealed class MonthlyClose : AuditableEntity
{
    private MonthlyClose()
    {
    }

    private MonthlyClose(
        Guid id,
        YearMonth month,
        Percentage percentage,
        MonthlySummaryResult summary,
        DateTime utcNow,
        string? description = null) : base(id, utcNow)
    {
        Month = month;
        CollaboratorPercentageBasisPoints = percentage.BasisPoints;
        IncomeMinorUnits = summary.IncomeMinorUnits;
        GoalMinorUnits = summary.GoalMinorUnits;
        BaseResultMinorUnits = summary.BaseResultMinorUnits;
        FundMinorUnits = summary.CollaboratorFundMinorUnits;
        RetainedResultMinorUnits = summary.RetainedResultMinorUnits;
        ClosedUtc = utcNow;
        Description = NormalizeOptionalText(description);
    }

    public YearMonth Month { get; private set; }

    public int CollaboratorPercentageBasisPoints { get; private set; }

    public long IncomeMinorUnits { get; private set; }

    public long GoalMinorUnits { get; private set; }

    public long BaseResultMinorUnits { get; private set; }

    public long FundMinorUnits { get; private set; }

    public long RetainedResultMinorUnits { get; private set; }

    public DateTime ClosedUtc { get; private set; }

    public DateTime? ReopenedUtc { get; private set; }

    public string? Description { get; private set; }

    public long AccountsReceivableMinorUnits { get; private set; }
    public long PaidOutflowsMinorUnits { get; private set; }
    public long AccountsPayableMinorUnits { get; private set; }
    public long NewReservesMinorUnits { get; private set; }
    public long CarriedReservesMinorUnits { get; private set; }
    public long ReserveAdjustmentsMinorUnits { get; private set; }
    public long LoanPaymentsMinorUnits { get; private set; }
    public long FinancingReceivedMinorUnits { get; private set; }
    public long PriorUncoveredCommitmentsMinorUnits { get; private set; }
    public long BreakEvenMinorUnits { get; private set; }
    public long ShortfallMinorUnits { get; private set; }

    public bool IsConfirmed => !ReopenedUtc.HasValue;

    public static MonthlyClose Create(
        YearMonth month,
        Percentage percentage,
        MonthlySummaryResult summary,
        DateTime utcNow,
        string? description = null) => new(Guid.NewGuid(), month, percentage, summary, utcNow, description);

    public static MonthlyClose Create(FinancialMonthSnapshot snapshot, DateTime utcNow, string? description = null)
    {
        var legacySummary = new MonthlySummaryResult(
            snapshot.CollectedOperatingIncomeMinorUnits, snapshot.BreakEvenMinorUnits,
            snapshot.ShortfallMinorUnits, snapshot.DistributableResultMinorUnits,
            snapshot.CollaboratorFundMinorUnits, snapshot.RetainedLocalMinorUnits);
        return new MonthlyClose(Guid.NewGuid(), snapshot.Month,
            Percentage.FromBasisPoints(snapshot.GlobalPercentageBasisPoints), legacySummary, utcNow, description)
        {
            AccountsReceivableMinorUnits = snapshot.AccountsReceivableMinorUnits,
            PaidOutflowsMinorUnits = snapshot.PaidOutflowsMinorUnits,
            AccountsPayableMinorUnits = snapshot.AccountsPayableMinorUnits,
            NewReservesMinorUnits = snapshot.NewReservesMinorUnits,
            CarriedReservesMinorUnits = snapshot.CarriedReservesMinorUnits,
            ReserveAdjustmentsMinorUnits = snapshot.ReserveAdjustmentsMinorUnits,
            LoanPaymentsMinorUnits = snapshot.LoanPaymentsMinorUnits,
            FinancingReceivedMinorUnits = snapshot.FinancingReceivedMinorUnits,
            PriorUncoveredCommitmentsMinorUnits = snapshot.PriorUncoveredCommitmentsMinorUnits,
            BreakEvenMinorUnits = snapshot.BreakEvenMinorUnits,
            ShortfallMinorUnits = snapshot.ShortfallMinorUnits,
        };
    }

    public FinancialMonthSnapshot ToFinancialSnapshot() => new(
        Month, IncomeMinorUnits, AccountsReceivableMinorUnits, PaidOutflowsMinorUnits,
        AccountsPayableMinorUnits, NewReservesMinorUnits, CarriedReservesMinorUnits,
        ReserveAdjustmentsMinorUnits, LoanPaymentsMinorUnits, FinancingReceivedMinorUnits,
        PriorUncoveredCommitmentsMinorUnits, BaseResultMinorUnits, BreakEvenMinorUnits,
        ShortfallMinorUnits, FundMinorUnits, RetainedResultMinorUnits,
        CollaboratorPercentageBasisPoints, []);

    public MonthlySummaryResult ToSummary() => new(
        IncomeMinorUnits,
        GoalMinorUnits,
        Math.Max(0, GoalMinorUnits - IncomeMinorUnits),
        BaseResultMinorUnits,
        FundMinorUnits,
        RetainedResultMinorUnits);

    public void Reopen(DateTime utcNow)
    {
        if (!IsConfirmed)
        {
            throw new InvalidOperationException("El cierre ya está reabierto.");
        }

        ReopenedUtc = utcNow;
        MarkUpdated(utcNow);
    }
}
