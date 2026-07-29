using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
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
    string Explanation,
    int ChargeableChairDays = 0,
    long SuggestedDailyPerChairMinorUnits = 0,
    long CurrentDailyMinorUnits = 0,
    long ProjectedChairIncomeMinorUnits = 0)
{
    public bool CanCalculate => OccupiedChairs > 0;
}

public static class SuggestedChairPriceCalculator
{
    public static SuggestedChairPrice Calculate(
        AdministrationData data,
        Money currentDailyRate,
        YearMonth month,
        DateOnly today)
    {
        bool InMonth(DateOnly date) => YearMonth.From(date) == month;
        long officialGoal = FinancialMonthCalculator.Calculate(
            data,
            Percentage.FromBasisPoints(0),
            month).BreakEvenMinorUnits;
        long unofficial = data.UnofficialExpenses
            .Where(item => item.AppliesOn(today))
            .Sum(item => item.MonthlyAmount.MinorUnits);
        long nonChairIncome = checked(
            data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Sale && InMonth(item.Date))
                .Sum(item => item.CashAmount?.MinorUnits ?? 0)
            + data.FinancialEntries.Where(item => item.Type == FinancialEntryType.OtherIncome && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits));
        long amountToCover = Math.Max(0, checked(officialGoal - nonChairIncome));
        int occupied = data.Chairs.Count(item => item.AssignedPersonId.HasValue
            && data.LocalUsePeople.Any(person => person.Id == item.AssignedPersonId && person.IsCurrentOn(today)));

        var chargeablePersonDays = new HashSet<(Guid PersonId, DateOnly Date)>();
        if (data.ChairAssignmentPeriods.Count > 0)
        {
            foreach (ChairAssignmentPeriod assignment in data.ChairAssignmentPeriods)
            {
                LocalUsePerson? person = data.LocalUsePeople.SingleOrDefault(
                    item => item.Id == assignment.PersonId);
                if (person is null) continue;
                for (DateOnly date = month.FirstDay; date <= month.LastDay; date = date.AddDays(1))
                {
                    if (assignment.AppliesOn(date)
                        && person.IsCurrentOn(date)
                        && DailyChargeCalculator.IsChargeableDay(date))
                    {
                        chargeablePersonDays.Add((person.Id, date));
                    }
                }
            }
        }
        else
        {
            foreach (Chair chair in data.Chairs.Where(item => item.AssignedPersonId.HasValue))
            {
                LocalUsePerson? person = data.LocalUsePeople.SingleOrDefault(
                    item => item.Id == chair.AssignedPersonId);
                if (person is null) continue;
                for (DateOnly date = month.FirstDay; date <= month.LastDay; date = date.AddDays(1))
                {
                    if (person.IsCurrentOn(date) && DailyChargeCalculator.IsChargeableDay(date))
                    {
                        chargeablePersonDays.Add((person.Id, date));
                    }
                }
            }
        }
        int chargeableDays = chargeablePersonDays.Count;
        long projectedChairIncome = data.DailyRates.Count == 0
            ? checked(chargeableDays * currentDailyRate.MinorUnits)
            : chargeablePersonDays.Sum(item =>
                DailyChargeCalculator.RateFor(data.DailyRates, item.Date).Amount.MinorUnits);
        long suggestedDaily = chargeableDays == 0
            ? 0
            : checked((long)decimal.Round(
                (decimal)amountToCover / chargeableDays,
                0,
                MidpointRounding.AwayFromZero));
        long monthly = occupied == 0
            ? 0
            : checked((long)decimal.Round((decimal)amountToCover / occupied, 0, MidpointRounding.AwayFromZero));
        long weekly = checked(suggestedDaily * 6);
        long currentMonthly = projectedChairIncome;

        string explanation = occupied == 0
            ? "No se puede calcular: no hay sillas ocupadas"
            : "Divide el faltante entre los días-silla cobrables de lunes a sábado; excluye domingos y respeta asignaciones.";

        return new SuggestedChairPrice(
            occupied,
            officialGoal,
            unofficial,
            nonChairIncome,
            amountToCover,
            monthly,
            weekly,
            checked(currentDailyRate.MinorUnits * 6),
            currentMonthly,
            explanation,
            chargeableDays,
            suggestedDaily,
            currentDailyRate.MinorUnits,
            projectedChairIncome);
    }
}
