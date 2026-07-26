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
    long CreditsMinorUnits,
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
        ServicesMinorUnits + TaxesMinorUnits + CreditsMinorUnits + OtherObligationsMinorUnits
        + MerchandiseMinorUnits + MandatorySuppliesMinorUnits + OptionalSuppliesMinorUnits
        + MaintenanceMinorUnits + UnexpectedMinorUnits + OtherExpensesMinorUnits
        + PendingPlansMinorUnits + HistoricalAdjustmentMinorUnits);
}

public sealed record AnnualAdministrationReport(
    AnnualBalanceResult Balance,
    MonthlyExpenseBreakdown Expenses,
    string Indicator);

public sealed record LocalUseEarning(DateOnly Date, DateTime OccurredUtc, long MinorUnits);

public sealed record MonthlyCashBreakdown(
    YearMonth Month,
    long CarryInMinorUnits,
    long LocalUseIncomeMinorUnits,
    long SalesIncomeMinorUnits,
    long OtherIncomeMinorUnits,
    long OtherRealIncomeMinorUnits,
    long CollaboratorContributionsMinorUnits,
    long FinancingReceivedMinorUnits,
    long InventoryMinorUnits,
    long GeneralExpensesMinorUnits,
    long RecurringExpensesMinorUnits,
    long UnexpectedExpensesMinorUnits,
    long ServicesMinorUnits,
    long TaxesMinorUnits,
    long OtherObligationsMinorUnits,
    long LoanPaymentsMinorUnits,
    long CreditPaymentsMinorUnits,
    long MaintenanceMinorUnits,
    long CollaboratorPaymentsMinorUnits,
    long OtherOutflowsMinorUnits,
    long TotalIncomeMinorUnits,
    long TotalSpentMinorUnits,
    long BreakEvenMinorUnits,
    long DifferenceMinorUnits,
    long CarryOutMinorUnits,
    bool IsClosed);

public static class AdministrationReports
{
    public static MonthlyCashBreakdown MonthlyCash(
        AdministrationData data,
        Percentage collaboratorPercentage,
        YearMonth month)
    {
        MonthlyClose? close = data.MonthlyCloses
            .Where(item => item.Month == month && item.IsConfirmed)
            .OrderByDescending(item => item.ClosedUtc)
            .FirstOrDefault();
        FinancialMonthSnapshot snapshot = close?.ToFinancialSnapshot()
            ?? FinancialMonthCalculator.Calculate(data, collaboratorPercentage, month);
        bool InMonth(DateOnly date) => YearMonth.From(date) == month;

        long localUse = EarnedLocalUseIncome(data)
            .Where(item => InMonth(item.Date))
            .Sum(item => item.MinorUnits);
        long sales = data.InventoryMovements
            .Where(item => item.Type == InventoryMovementType.Sale && InMonth(item.Date))
            .Sum(item => item.CashAmount?.MinorUnits ?? 0);
        long otherIncome = data.FinancialEntries
            .Where(item => item.Type == FinancialEntryType.OtherIncome && InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits);
        long otherRealIncome = checked(
            snapshot.CollectedOperatingIncomeMinorUnits - localUse - sales - otherIncome);
        long contributions = data.CollaboratorContributions
            .Where(item => InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits);
        long financing = data.Loans
            .Where(item => InMonth(item.StartDate))
            .Sum(item => item.InitialBalance.MinorUnits);

        MonthlyExpenseBreakdown expenses = MonthlyExpenses(data, month);
        long recurring = data.UnofficialExpenses
            .Where(item => item.AppliesInMonth(month))
            .Sum(item => item.MonthlyAmount.MinorUnits);
        long services = expenses.ServicesMinorUnits;
        long taxes = expenses.TaxesMinorUnits;
        long credits = expenses.CreditsMinorUnits;
        long otherObligations = expenses.OtherObligationsMinorUnits;
        long maintenance = expenses.MaintenanceMinorUnits;
        long loans = snapshot.LoanPaymentsMinorUnits;

        foreach (FinancialCommitmentCandidate candidate in snapshot.Candidates.Where(item => !item.IsExcluded))
        {
            if (candidate.SourceType == FinancialCommitmentSource.Obligation)
            {
                ObligationType type = data.Obligations
                    .Single(item => item.Id == candidate.SourceId).Type;
                switch (type)
                {
                    case ObligationType.Service: services += candidate.ExpectedMinorUnits; break;
                    case ObligationType.Tax: taxes += candidate.ExpectedMinorUnits; break;
                    case ObligationType.Credit: credits += candidate.ExpectedMinorUnits; break;
                    default: otherObligations += candidate.ExpectedMinorUnits; break;
                }
            }
            else if (candidate.SourceType == FinancialCommitmentSource.Maintenance)
            {
                maintenance += candidate.ExpectedMinorUnits;
            }
            else if (candidate.SourceType == FinancialCommitmentSource.LoanInstallment)
            {
                loans += candidate.ExpectedMinorUnits;
            }
        }

        foreach (Obligation annual in data.Obligations.Where(item =>
                     item.Recurrence == RecurrenceFrequency.Annual && item.DueDate.Year == month.Year))
        {
            long provision = ProratedAnnualAmount(annual.ExpectedAmount.MinorUnits, month.Month);
            switch (annual.Type)
            {
                case ObligationType.Service: services += provision; break;
                case ObligationType.Tax: taxes += provision; break;
                case ObligationType.Credit: credits += provision; break;
                default: otherObligations += provision; break;
            }
        }
        foreach (ObligationPayment payment in data.ObligationPayments.Where(item => InMonth(item.Date)))
        {
            Obligation annual = data.Obligations.Single(item => item.Id == payment.ObligationId);
            if (annual.Recurrence != RecurrenceFrequency.Annual) continue;
            long adjustment = payment.Amount.MinorUnits - annual.ExpectedAmount.MinorUnits;
            switch (annual.Type)
            {
                case ObligationType.Service: services += adjustment; break;
                case ObligationType.Tax: taxes += adjustment; break;
                case ObligationType.Credit: credits += adjustment; break;
                default: otherObligations += adjustment; break;
            }
        }

        long inventory = checked(
            expenses.MerchandiseMinorUnits + expenses.MandatorySuppliesMinorUnits
            + expenses.OptionalSuppliesMinorUnits + expenses.PendingPlansMinorUnits);
        long generalExpenses = expenses.OtherExpensesMinorUnits;
        long collaboratorPayments = data.DistributionPayments
            .Where(item => InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits);
        long knownWithoutAdjustment = checked(
            inventory + generalExpenses + recurring + expenses.UnexpectedMinorUnits
            + services + taxes + otherObligations + loans + credits + maintenance);
        long otherOutflows = checked(snapshot.BreakEvenMinorUnits - knownWithoutAdjustment);
        long totalSpent = checked(snapshot.BreakEvenMinorUnits + collaboratorPayments);
        long carryIn = CalculateCarryIn(data, collaboratorPercentage, month);
        long totalIncome = checked(
            carryIn + localUse + sales + otherIncome + otherRealIncome);
        long difference = checked(totalIncome - totalSpent);
        long carryOut = checked(difference + contributions + financing);

        return new MonthlyCashBreakdown(
            month,
            carryIn,
            localUse,
            sales,
            otherIncome,
            otherRealIncome,
            contributions,
            financing,
            inventory,
            generalExpenses,
            recurring,
            expenses.UnexpectedMinorUnits,
            services,
            taxes,
            otherObligations,
            loans,
            credits,
            maintenance,
            collaboratorPayments,
            otherOutflows,
            totalIncome,
            totalSpent,
            totalSpent,
            difference,
            carryOut,
            close is not null);
    }

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
            data.LocalUsePayments.Where(item => InMonth(item.PaymentDate)).Sum(item => item.Amount.MinorUnits),
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
        var totalExpenses = new MonthlyExpenseBreakdown(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        var summaries = new List<MonthlySummaryResult>();
        foreach (int monthNumber in Enumerable.Range(1, 12))
        {
            var month = new YearMonth(year, monthNumber);
            MonthlySummaryResult summary = MonthlySummary(data, collaboratorPercentage, month);
            MonthlyExpenseBreakdown dynamicBreakdown = MonthlyExpenses(data, month);
            long target = CanonicalExpenseTarget(data, collaboratorPercentage, month);
            long adjustment = target - dynamicBreakdown.TotalMinorUnits;
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
        long pending = data.Obligations.Where(item => item.DueDate.Year == year)
            .Sum(item => item.OutstandingAmount(data.ObligationPayments).MinorUnits);
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
                && data.Obligations.Any(item => item.Id == payment.ObligationId
                    && item.Type == type
                    && item.Recurrence != RecurrenceFrequency.Annual))
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
        long pendingPlans = data.MonthlyPurchaseItems
            .Where(item => item.Month == month
                && MonthlyPurchaseCommitmentPolicy.IsPending(item, data, month.LastDay))
            .Sum(item => item.ExpectedTotalMinorUnits);

        return new MonthlyExpenseBreakdown(
            Obligations(ObligationType.Service),
            Obligations(ObligationType.Tax),
            Obligations(ObligationType.Credit),
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
            pendingPlans,
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

    private static long CalculateCarryIn(
        AdministrationData data,
        Percentage collaboratorPercentage,
        YearMonth target)
    {
        AnnualCarryover? annual = data.AnnualCarryovers
            .Where(item => item.TargetYear == target.Year)
            .OrderByDescending(item => item.CreatedUtc)
            .FirstOrDefault();
        long carry = annual is null
            ? 0
            : checked(annual.SurplusMinorUnits - annual.DeficitMinorUnits);
        foreach (int monthNumber in Enumerable.Range(1, target.Month - 1))
        {
            var month = new YearMonth(target.Year, monthNumber);
            MonthlyClose? close = data.MonthlyCloses
                .Where(item => item.Month == month && item.IsConfirmed)
                .OrderByDescending(item => item.ClosedUtc)
                .FirstOrDefault();
            FinancialMonthSnapshot snapshot = close?.ToFinancialSnapshot()
                ?? FinancialMonthCalculator.Calculate(data, collaboratorPercentage, month);
            long collaboratorPayments = data.DistributionPayments
                .Where(item => YearMonth.From(item.Date) == month)
                .Sum(item => item.Amount.MinorUnits);
            carry = checked(
                carry + snapshot.CollectedOperatingIncomeMinorUnits
                + snapshot.FinancingReceivedMinorUnits
                - snapshot.BreakEvenMinorUnits
                - collaboratorPayments);
        }

        return carry;
    }

    private static long ProratedAnnualAmount(long annualMinorUnits, int month)
    {
        long regular = annualMinorUnits / 12;
        return month == 12 ? checked(annualMinorUnits - regular * 11) : regular;
    }

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

    private static long CanonicalExpenseTarget(
        AdministrationData data,
        Percentage collaboratorPercentage,
        YearMonth month)
    {
        MonthlyClose? confirmed = data.MonthlyCloses
            .Where(item => item.Month == month && item.IsConfirmed)
            .OrderByDescending(item => item.ClosedUtc)
            .FirstOrDefault();
        return confirmed?.ToFinancialSnapshot().BreakEvenMinorUnits
            ?? FinancialMonthCalculator.Calculate(data, collaboratorPercentage, month).BreakEvenMinorUnits;
    }

    private static MonthlyExpenseBreakdown Add(MonthlyExpenseBreakdown left, MonthlyExpenseBreakdown right) => new(
        left.ServicesMinorUnits + right.ServicesMinorUnits,
        left.TaxesMinorUnits + right.TaxesMinorUnits,
        left.CreditsMinorUnits + right.CreditsMinorUnits,
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
