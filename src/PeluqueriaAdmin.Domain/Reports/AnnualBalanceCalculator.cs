namespace PeluqueriaAdmin.Domain.Reports;

public sealed record AnnualBalanceResult(
    long IncomeMinorUnits,
    long ExpenseMinorUnits,
    long DistributionMinorUnits,
    long RetainedMinorUnits,
    long PendingMinorUnits,
    long MissingMinorUnits);

public static class AnnualBalanceCalculator
{
    public static AnnualBalanceResult Calculate(
        IEnumerable<MonthlySummaryResult> months,
        long paidDistributionsMinorUnits,
        long pendingMinorUnits)
    {
        MonthlySummaryResult[] values = months.ToArray();
        long income = values.Sum(month => month.IncomeMinorUnits);
        long expense = values.Sum(month => month.GoalMinorUnits);
        long retained = values.Sum(month => month.RetainedResultMinorUnits);
        return new AnnualBalanceResult(
            income,
            expense,
            paidDistributionsMinorUnits,
            retained,
            pendingMinorUnits,
            Math.Max(0, -retained));
    }
}
