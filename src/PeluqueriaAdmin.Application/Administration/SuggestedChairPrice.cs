using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Administration;

public sealed record SuggestedChairPrice(
    int OccupiedChairs,
    long OfficialGoalMinorUnits,
    long UnofficialExpensesMinorUnits,
    long ExpectedNonChairIncomeMinorUnits,
    long AmountToCoverMinorUnits,
    long SuggestedMonthlyPerChairMinorUnits,
    long SuggestedWeeklyPerChairMinorUnits,
    long CurrentWeeklyMinorUnits,
    long CurrentMonthlyEquivalentMinorUnits,
    string Explanation)
{
    public bool CanCalculate => OccupiedChairs > 0;
}

public static class SuggestedChairPriceCalculator
{
    public static SuggestedChairPrice Calculate(
        AdministrationData data,
        Money currentWeeklyRate,
        YearMonth month,
        DateOnly today)
    {
        bool InMonth(DateOnly date) => YearMonth.From(date) == month;
        long officialGoal = checked(
            data.Obligations.Where(item => InMonth(item.DueDate)).Sum(item => item.ExpectedAmount.MinorUnits)
            + data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Purchase && InMonth(item.Date))
                .Sum(item => item.CashAmount?.MinorUnits ?? 0)
            + data.FinancialEntries.Where(item => item.Type is FinancialEntryType.Expense or FinancialEntryType.UnexpectedExpense && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits)
            + data.MaintenanceRecords.Where(item => InMonth(item.ScheduledDate))
                .Sum(item => item.ActualCost?.MinorUnits ?? item.EstimatedCost?.MinorUnits ?? 0));
        long unofficial = data.UnofficialExpenses
            .Where(item => item.AppliesOn(today))
            .Sum(item => item.MonthlyAmount.MinorUnits);
        long nonChairIncome = checked(
            data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Sale && InMonth(item.Date))
                .Sum(item => item.CashAmount?.MinorUnits ?? 0)
            + data.FinancialEntries.Where(item => item.Type == FinancialEntryType.OtherIncome && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits));
        long amountToCover = Math.Max(0, checked(officialGoal + unofficial - nonChairIncome));
        int occupied = data.Chairs.Count(item => item.AssignedPersonId.HasValue
            && data.LocalUsePeople.Any(person => person.Id == item.AssignedPersonId && person.IsCurrentOn(today)));

        long monthly = occupied == 0
            ? 0
            : checked((long)decimal.Round((decimal)amountToCover / occupied, 0, MidpointRounding.AwayFromZero));
        long weekly = checked((long)decimal.Round(monthly * 12m / 52m, 0, MidpointRounding.AwayFromZero));
        long currentMonthly = checked((long)decimal.Round(
            currentWeeklyRate.MinorUnits * 52m / 12m,
            0,
            MidpointRounding.AwayFromZero));

        string explanation = occupied == 0
            ? "No se puede calcular: no hay sillas ocupadas"
            : "Incluye la meta mensual oficial y los gastos extraoficiales vigentes; resta ventas y otros ingresos esperados. No resta los pagos actuales por sillas.";

        return new SuggestedChairPrice(
            occupied,
            officialGoal,
            unofficial,
            nonChairIncome,
            amountToCover,
            monthly,
            weekly,
            currentWeeklyRate.MinorUnits,
            currentMonthly,
            explanation);
    }
}
