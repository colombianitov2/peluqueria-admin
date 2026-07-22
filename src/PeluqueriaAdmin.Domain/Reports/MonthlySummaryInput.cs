namespace PeluqueriaAdmin.Domain.Reports;

public sealed record MonthlySummaryInput(
    long LocalUseIncomeMinorUnits,
    long GrossSalesMinorUnits,
    long OtherIncomeMinorUnits,
    long ObligationGoalMinorUnits,
    long MerchandisePurchasesMinorUnits,
    long MandatoryExpensesMinorUnits,
    long OptionalSuppliesActualMinorUnits,
    long UnexpectedExpensesMinorUnits,
    long MaintenanceGoalMinorUnits,
    long PendingApprovedPlansMinorUnits)
{
    public MonthlySummaryInput(
        long localUseIncomeMinorUnits,
        long grossSalesMinorUnits,
        long otherIncomeMinorUnits,
        long obligationGoalMinorUnits,
        long merchandisePurchasesMinorUnits,
        long mandatoryExpensesMinorUnits,
        long optionalSuppliesActualMinorUnits,
        long OptionalSuppliesBudgetMinorUnits,
        long unexpectedExpensesMinorUnits,
        long maintenanceGoalMinorUnits,
        long pendingApprovedPlansMinorUnits)
        : this(
            localUseIncomeMinorUnits,
            grossSalesMinorUnits,
            otherIncomeMinorUnits,
            obligationGoalMinorUnits,
            merchandisePurchasesMinorUnits,
            mandatoryExpensesMinorUnits,
            optionalSuppliesActualMinorUnits,
            unexpectedExpensesMinorUnits,
            maintenanceGoalMinorUnits,
            pendingApprovedPlansMinorUnits)
    {
        _ = OptionalSuppliesBudgetMinorUnits;
    }
}
