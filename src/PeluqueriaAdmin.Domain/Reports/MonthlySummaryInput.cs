namespace PeluqueriaAdmin.Domain.Reports;

public sealed record MonthlySummaryInput(
    long LocalUseIncomeMinorUnits,
    long GrossSalesMinorUnits,
    long OtherIncomeMinorUnits,
    long ObligationGoalMinorUnits,
    long MerchandisePurchasesMinorUnits,
    long MandatoryExpensesMinorUnits,
    long OptionalSuppliesActualMinorUnits,
    long OptionalSuppliesBudgetMinorUnits,
    long UnexpectedExpensesMinorUnits,
    long MaintenanceGoalMinorUnits,
    long PendingApprovedPlansMinorUnits);
