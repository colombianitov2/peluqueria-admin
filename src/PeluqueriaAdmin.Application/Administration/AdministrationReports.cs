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

public static class AdministrationReports
{
    public static MonthlySummaryResult MonthlySummary(
        AdministrationData data,
        Money optionalSuppliesBudget,
        Percentage collaboratorPercentage,
        YearMonth month)
    {
        MonthlyClose? confirmed = data.MonthlyCloses
            .Where(item => item.Month == month && item.IsConfirmed)
            .OrderByDescending(item => item.ClosedUtc)
            .FirstOrDefault();
        return confirmed?.ToSummary() ?? MonthlySummaryCalculator.Calculate(
            BuildMonthlyInput(data, optionalSuppliesBudget, month),
            collaboratorPercentage);
    }

    public static MonthlySummaryInput BuildMonthlyInput(
        AdministrationData data,
        Money optionalSuppliesBudget,
        YearMonth month)
    {
        bool InMonth(DateOnly date) => YearMonth.From(date) == month;
        InventoryMovement[] purchases = data.InventoryMovements
            .Where(item => item.Type == InventoryMovementType.Purchase && InMonth(item.Date))
            .ToArray();
        long PurchaseFor(params ProductCategory[] categories) => purchases
            .Where(item => data.Products.Any(product => product.Id == item.ProductId
                && categories.Contains(product.Category)))
            .Sum(item => item.CashAmount?.MinorUnits ?? 0);
        long planCost = CalculatePlanCost(data, month);

        return new MonthlySummaryInput(
            data.LocalUsePayments.Where(item => InMonth(item.PaymentDate)).Sum(item => item.Amount.MinorUnits),
            data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Sale && InMonth(item.Date))
                .Sum(item => item.CashAmount?.MinorUnits ?? 0),
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.OtherIncome && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits),
            data.Obligations.Where(item => InMonth(item.DueDate))
                .Sum(item => item.GoalAmount(data.ObligationPayments).MinorUnits),
            PurchaseFor(ProductCategory.FoodOrDrinkForSale, ProductCategory.OtherProductForSale),
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.Expense
                    && item.Category != ExpenseCategory.OptionalSupply && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits)
                + PurchaseFor(ProductCategory.Cleaning, ProductCategory.LocalSupply, ProductCategory.OtherLocalProduct),
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.Expense
                    && item.Category == ExpenseCategory.OptionalSupply && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits)
                + PurchaseFor(ProductCategory.CustomerCourtesy),
            optionalSuppliesBudget.MinorUnits,
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.UnexpectedExpense && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits),
            data.MaintenanceRecords.Sum(item => item.GoalAmountFor(month).MinorUnits),
            planCost);
    }

    public static AnnualAdministrationReport Annual(
        AdministrationData data,
        Money optionalSuppliesBudget,
        Percentage collaboratorPercentage,
        int year)
    {
        var totalExpenses = new MonthlyExpenseBreakdown(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var summaries = new List<MonthlySummaryResult>();
        foreach (int monthNumber in Enumerable.Range(1, 12))
        {
            var month = new YearMonth(year, monthNumber);
            MonthlySummaryResult summary = MonthlySummary(data, optionalSuppliesBudget, collaboratorPercentage, month);
            MonthlyExpenseBreakdown dynamicBreakdown = MonthlyExpenses(data, optionalSuppliesBudget, month);
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
        Money optionalSuppliesBudget,
        YearMonth month)
    {
        bool InMonth(DateOnly date) => YearMonth.From(date) == month;
        long Obligations(ObligationType type) => data.Obligations
            .Where(item => item.Type == type && InMonth(item.DueDate))
            .Sum(item => item.GoalAmount(data.ObligationPayments).MinorUnits);
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
            Math.Max(optionalSuppliesBudget.MinorUnits, optionalActual),
            data.MaintenanceRecords.Sum(item => item.GoalAmountFor(month).MinorUnits),
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.UnexpectedExpense && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits),
            Expenses(ExpenseCategory.Other) + Purchases(ProductCategory.OtherLocalProduct),
            CalculatePlanCost(data, month),
            0);
    }

    private static long CalculatePlanCost(AdministrationData data, YearMonth month) =>
        data.RestockPlans.Where(item => item.Month == month).Sum(plan =>
        {
            InventoryMovement[] movements = data.InventoryMovements
                .Where(item => item.ProductId == plan.ProductId && item.Date <= month.LastDay)
                .ToArray();
            decimal suggested = plan.SuggestedPurchase(InventoryCalculator.CurrentQuantity(movements));
            return checked((long)decimal.Round(
                InventoryCalculator.AverageUnitCost(movements).MinorUnits * suggested,
                0,
                MidpointRounding.AwayFromZero));
        });

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
