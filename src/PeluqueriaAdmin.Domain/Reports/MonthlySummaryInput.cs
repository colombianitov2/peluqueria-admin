namespace PeluqueriaAdmin.Domain.Reports;

public sealed record MonthlySummaryInput(
    long LocalUseEarnedIncomeMinorUnits,
    long GrossSalesMinorUnits,
    long OtherIncomeMinorUnits,
    long InventoryPurchasesMinorUnits,
    long RegisteredExpensesMinorUnits,
    long UnexpectedExpensesMinorUnits,
    long ObligationPaymentsMinorUnits,
    long CompletedMaintenanceMinorUnits);
