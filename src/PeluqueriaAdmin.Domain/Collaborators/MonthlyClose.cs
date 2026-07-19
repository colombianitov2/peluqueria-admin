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

    public bool IsConfirmed => !ReopenedUtc.HasValue;

    public static MonthlyClose Create(
        YearMonth month,
        Percentage percentage,
        MonthlySummaryResult summary,
        DateTime utcNow,
        string? description = null) => new(Guid.NewGuid(), month, percentage, summary, utcNow, description);

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
