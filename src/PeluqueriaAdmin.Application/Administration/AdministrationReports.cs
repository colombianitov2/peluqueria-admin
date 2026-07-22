using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Administration;

public sealed record MonthlyExpenseBreakdown(
    long ServicesMinorUnits,
    long TaxesMinorUnits,
    long OtherObligationsMinorUnits,
    long MerchandiseMinorUnits,
    long MandatorySuppliesMinorUnits,
    long OptionalSuppliesMinorUnits,
    long MaintenanceMinorUnits,
    long UnexpectedMinorUnits,
    long OtherExpensesMinorUnits,
    long PendingPlansMinorUnits,
    long HistoricalAdjustmentMinorUnits)
{
    public long TotalMinorUnits => checked(
        ServicesMinorUnits + TaxesMinorUnits + OtherObligationsMinorUnits
        + MerchandiseMinorUnits + MandatorySuppliesMinorUnits + OptionalSuppliesMinorUnits
        + MaintenanceMinorUnits + UnexpectedMinorUnits + OtherExpensesMinorUnits
        + PendingPlansMinorUnits + HistoricalAdjustmentMinorUnits);
}

public sealed record AnnualAdministrationReport(
    AnnualBalanceResult Balance,
    MonthlyExpenseBreakdown Expenses,
    string Indicator);

public sealed record LocalUseEarning(DateOnly Date, DateTime OccurredUtc, long MinorUnits);

public static class AdministrationReports
{
    public static MonthlySummaryResult MonthlySummary(
        AdministrationData data,
        Percentage collaboratorPercentage,
        YearMonth month)
    {
        MonthlyClose? confirmed = data.MonthlyCloses
            .Where(item => item.Month == month && item.IsConfirmed)
            .OrderByDescending(item => item.ClosedUtc)
            .FirstOrDefault();
        return confirmed?.ToSummary() ?? MonthlySummaryCalculator.Calculate(
            BuildMonthlyInput(data, month),
            collaboratorPercentage);
    }

    public static MonthlySummaryInput BuildMonthlyInput(
        AdministrationData data,
        YearMonth month)
    {
        bool InMonth(DateOnly date) => YearMonth.From(date) == month;
        InventoryMovement[] purchases = data.InventoryMovements
            .Where(item => item.Type == InventoryMovementType.Purchase && InMonth(item.Date))
            .ToArray();
        return new MonthlySummaryInput(
            CalculateEarnedLocalUseIncome(data, month),
            data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Sale && InMonth(item.Date))
                .Sum(item => item.CashAmount?.MinorUnits ?? 0),
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.OtherIncome && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits),
            purchases.Sum(item => item.CashAmount?.MinorUnits ?? 0),
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.Expense && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits),
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.UnexpectedExpense && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits),
            data.ObligationPayments.Where(item => InMonth(item.Date)).Sum(item => item.Amount.MinorUnits),
            data.MaintenanceRecords.Where(item => item.CompletedDate.HasValue && InMonth(item.CompletedDate.Value))
                .Sum(item => item.ActualCost?.MinorUnits ?? 0));
    }

    public static AnnualAdministrationReport Annual(
        AdministrationData data,
        Percentage collaboratorPercentage,
        int year)
    {
        var totalExpenses = new MonthlyExpenseBreakdown(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var summaries = new List<MonthlySummaryResult>();
        foreach (int monthNumber in Enumerable.Range(1, 12))
        {
            var month = new YearMonth(year, monthNumber);
            MonthlySummaryResult summary = MonthlySummary(data, collaboratorPercentage, month);
            MonthlyExpenseBreakdown dynamicBreakdown = MonthlyExpenses(data, month);
            long adjustment = summary.GoalMinorUnits - dynamicBreakdown.TotalMinorUnits;
            totalExpenses = Add(totalExpenses, dynamicBreakdown with { HistoricalAdjustmentMinorUnits = adjustment });
            summaries.Add(summary);
        }

        Guid[] confirmedCloseIds = data.MonthlyCloses
            .Where(item => item.IsConfirmed && item.Month.Year == year)
            .Select(item => item.Id)
            .ToArray();
        Guid[] validParticipantIds = data.MonthlyCloseParticipants
            .Where(item => confirmedCloseIds.Contains(item.CloseId))
            .Select(item => item.Id)
            .ToArray();
        long distributions = data.DistributionPayments
            .Where(item => item.Date.Year == year && validParticipantIds.Contains(item.ParticipantId))
            .Sum(item => item.Amount.MinorUnits);
        long pending = data.Obligations.Where(item => item.DueDate.Year == year).Sum(item => Math.Max(
            0,
            item.ExpectedAmount.MinorUnits - data.ObligationPayments
                .Where(payment => payment.ObligationId == item.Id)
                .Sum(payment => payment.Amount.MinorUnits)));
        AnnualBalanceResult balance = AnnualBalanceCalculator.Calculate(summaries, distributions, pending);
        return new AnnualAdministrationReport(
            balance,
            totalExpenses,
            balance.RetainedMinorUnits >= 0 ? "Positivo" : "Negativo");
    }

    public static MonthlyExpenseBreakdown MonthlyExpenses(
        AdministrationData data,
        YearMonth month)
    {
        bool InMonth(DateOnly date) => YearMonth.From(date) == month;
        long Obligations(ObligationType type) => data.ObligationPayments
            .Where(payment => InMonth(payment.Date)
                && data.Obligations.Any(item => item.Id == payment.ObligationId && item.Type == type))
            .Sum(item => item.Amount.MinorUnits);
        long Purchases(params ProductCategory[] categories) => data.InventoryMovements
            .Where(item => item.Type == InventoryMovementType.Purchase && InMonth(item.Date)
                && data.Products.Any(product => product.Id == item.ProductId
                    && categories.Contains(product.Category)))
            .Sum(item => item.CashAmount?.MinorUnits ?? 0);
        long Expenses(ExpenseCategory category) => data.FinancialEntries
            .Where(item => item.Type == FinancialEntryType.Expense && item.Category == category && InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits);
        long optionalActual = Purchases(ProductCategory.CustomerCourtesy) + Expenses(ExpenseCategory.OptionalSupply);

        return new MonthlyExpenseBreakdown(
            Obligations(ObligationType.Service),
            Obligations(ObligationType.Tax),
            Obligations(ObligationType.OtherRecurring),
            Purchases(ProductCategory.FoodOrDrinkForSale, ProductCategory.OtherProductForSale)
                + Expenses(ExpenseCategory.MerchandisePurchase),
            Purchases(ProductCategory.Cleaning, ProductCategory.LocalSupply)
                + Expenses(ExpenseCategory.MandatorySupply),
            optionalActual,
            data.MaintenanceRecords.Where(item => item.CompletedDate.HasValue && InMonth(item.CompletedDate.Value))
                .Sum(item => item.ActualCost?.MinorUnits ?? 0),
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.UnexpectedExpense && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits),
            Expenses(ExpenseCategory.Other) + Purchases(ProductCategory.OtherLocalProduct),
            0,
            0);
    }

    public static IReadOnlyList<LocalUseEarning> EarnedLocalUseIncome(AdministrationData data)
    {
        var earnings = new List<LocalUseEarning>();
        foreach (var person in data.LocalUsePeople)
        {
            var charges = data.WeeklyCharges
                .Where(item => item.PersonId == person.Id)
                .OrderBy(item => item.PeriodEnd)
                .ThenBy(item => item.CreatedUtc)
                .Select(item => new RemainingCharge(item.PeriodEnd, item.CreatedUtc, item.Amount.MinorUnits))
                .ToArray();
            var payments = data.LocalUsePayments
                .Where(item => item.PersonId == person.Id)
                .OrderBy(item => item.PaymentDate)
                .ThenBy(item => item.CreatedUtc)
                .Select(item => new RemainingPayment(item.PaymentDate, item.CreatedUtc, item.Amount.MinorUnits))
                .ToArray();
            int chargeIndex = 0;
            int paymentIndex = 0;
            while (chargeIndex < charges.Length && paymentIndex < payments.Length)
            {
                long applied = Math.Min(charges[chargeIndex].Remaining, payments[paymentIndex].Remaining);
                DateOnly recognitionDate = charges[chargeIndex].PeriodEnd > payments[paymentIndex].Date
                    ? charges[chargeIndex].PeriodEnd
                    : payments[paymentIndex].Date;
                DateTime occurredUtc = charges[chargeIndex].PeriodEnd > payments[paymentIndex].Date
                    ? charges[chargeIndex].CreatedUtc
                    : payments[paymentIndex].CreatedUtc;
                earnings.Add(new LocalUseEarning(recognitionDate, occurredUtc, applied));
                charges[chargeIndex].Remaining -= applied;
                payments[paymentIndex].Remaining -= applied;
                if (charges[chargeIndex].Remaining == 0) chargeIndex++;
                if (payments[paymentIndex].Remaining == 0) paymentIndex++;
            }
        }
        return earnings;
    }


    private static long CalculateEarnedLocalUseIncome(AdministrationData data, YearMonth month) =>
        EarnedLocalUseIncome(data).Where(item => YearMonth.From(item.Date) == month).Sum(item => item.MinorUnits);

    private sealed class RemainingCharge(DateOnly periodEnd, DateTime createdUtc, long remaining)
    {
        public DateOnly PeriodEnd { get; } = periodEnd;
        public DateTime CreatedUtc { get; } = createdUtc;
        public long Remaining { get; set; } = remaining;
    }

    private sealed class RemainingPayment(DateOnly date, DateTime createdUtc, long remaining)
    {
        public DateOnly Date { get; } = date;
        public DateTime CreatedUtc { get; } = createdUtc;
        public long Remaining { get; set; } = remaining;
    }

    private static MonthlyExpenseBreakdown Add(MonthlyExpenseBreakdown left, MonthlyExpenseBreakdown right) => new(
        left.ServicesMinorUnits + right.ServicesMinorUnits,
        left.TaxesMinorUnits + right.TaxesMinorUnits,
        left.OtherObligationsMinorUnits + right.OtherObligationsMinorUnits,
        left.MerchandiseMinorUnits + right.MerchandiseMinorUnits,
        left.MandatorySuppliesMinorUnits + right.MandatorySuppliesMinorUnits,
        left.OptionalSuppliesMinorUnits + right.OptionalSuppliesMinorUnits,
        left.MaintenanceMinorUnits + right.MaintenanceMinorUnits,
        left.UnexpectedMinorUnits + right.UnexpectedMinorUnits,
        left.OtherExpensesMinorUnits + right.OtherExpensesMinorUnits,
        left.PendingPlansMinorUnits + right.PendingPlansMinorUnits,
        left.HistoricalAdjustmentMinorUnits + right.HistoricalAdjustmentMinorUnits);
}
