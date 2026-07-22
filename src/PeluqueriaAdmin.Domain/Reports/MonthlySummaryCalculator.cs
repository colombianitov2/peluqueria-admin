using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Reports;

public static class MonthlySummaryCalculator
{
    public static MonthlySummaryResult Calculate(MonthlySummaryInput input, Percentage collaboratorPercentage)
    {
        ArgumentNullException.ThrowIfNull(input);
        EnsureNonNegative(input);

        long income = checked(
            input.LocalUseIncomeMinorUnits
            + input.GrossSalesMinorUnits
            + input.OtherIncomeMinorUnits);
        long goal = checked(
            input.ObligationGoalMinorUnits
            + input.MerchandisePurchasesMinorUnits
            + input.MandatoryExpensesMinorUnits
            + input.OptionalSuppliesActualMinorUnits
            + input.UnexpectedExpensesMinorUnits
            + input.MaintenanceGoalMinorUnits
            + input.PendingApprovedPlansMinorUnits);
        long baseResult = income - goal;
        long fund = baseResult > 0
            ? checked((long)decimal.Round(
                baseResult * collaboratorPercentage.BasisPoints / 10_000m,
                0,
                MidpointRounding.AwayFromZero))
            : 0;

        return new MonthlySummaryResult(
            income,
            goal,
            Math.Max(0, goal - income),
            baseResult,
            fund,
            baseResult - fund);
    }

    private static void EnsureNonNegative(MonthlySummaryInput input)
    {
        if (input.GetType().GetProperties()
            .Where(property => property.PropertyType == typeof(long))
            .Any(property => (long)property.GetValue(input)! < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(input), "Los componentes del resumen no pueden ser negativos.");
        }
    }
}
