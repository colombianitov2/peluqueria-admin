namespace PeluqueriaAdmin.Domain.Reports;

public sealed record MonthlySummaryResult(
    long IncomeMinorUnits,
    long GoalMinorUnits,
    long MissingMinorUnits,
    long BaseResultMinorUnits,
    long CollaboratorFundMinorUnits,
    long RetainedResultMinorUnits);
