using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Administration;

public static class FinancialMonthCalculator
{
    public static FinancialMonthSnapshot Calculate(
        AdministrationData data,
        Percentage globalPercentage,
        YearMonth month)
    {
        DateOnly end = month.LastDay;
        bool InMonth(DateOnly date) => YearMonth.From(date) == month;

        long localUseCollected = AdministrationReports.EarnedLocalUseIncome(data)
            .Where(item => InMonth(item.Date))
            .Sum(item => item.MinorUnits);
        long sales = data.InventoryMovements
            .Where(item => item.Type == InventoryMovementType.Sale && InMonth(item.Date))
            .Sum(item => item.CashAmount?.MinorUnits ?? 0);
        long otherIncome = data.FinancialEntries
            .Where(item => item.Type == FinancialEntryType.OtherIncome && InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits);
        long collectedIncome = checked(localUseCollected + sales + otherIncome);

        long accountsReceivable = CalculateLocalUseDebt(data, end);
        IReadOnlyList<FinancialCommitmentCandidate> candidates = BuildCandidates(data, month, end);
        long annualAccountsPayable = data.Obligations
            .Where(item => item.Recurrence == RecurrenceFrequency.Annual && item.DueDate <= end)
            .Sum(item => ObligationOutstandingAt(item, data.ObligationPayments, end));
        long accountsPayable = checked(
            candidates.Sum(item => item.ExpectedMinorUnits) + annualAccountsPayable);

        FinancialReserve[] activeReserves = data.FinancialReserves
            .Where(item => ReserveIsActiveAt(item, end))
            .ToArray();
        long carriedReserves = activeReserves
            .Where(item => item.Month.Year < month.Year
                || item.Month.Year == month.Year && item.Month.Month < month.Month)
            .Sum(item => item.ReservedAmount.MinorUnits);
        long newReserves = candidates
            .Where(item => !item.IsExcluded
                && !activeReserves.Any(reserve =>
                    reserve.SourceType == item.SourceType && reserve.SourceId == item.SourceId))
            .Sum(item => item.ExpectedMinorUnits);

        Dictionary<(FinancialCommitmentSource, Guid), long> actualsBySource = ActualsBySource(data, month);
        long reserveAdjustments = 0;
        var reservedActualIds = new HashSet<(FinancialCommitmentSource, Guid)>();
        foreach (FinancialReserve reserve in data.FinancialReserves)
        {
            if (!actualsBySource.TryGetValue((reserve.SourceType, reserve.SourceId), out long actual))
            {
                continue;
            }

            reserveAdjustments = checked(
                reserveAdjustments + actual - reserve.ReservedAmount.MinorUnits);
            reservedActualIds.Add((reserve.SourceType, reserve.SourceId));
        }

        long purchases = data.InventoryMovements
            .Where(item => item.Type == InventoryMovementType.Purchase
                && InMonth(item.Date)
                && !data.MonthlyPurchaseItems.Any(plan =>
                    plan.PurchaseMovementId == item.Id
                    && reservedActualIds.Contains((FinancialCommitmentSource.MonthlyPurchase, plan.Id))))
            .Sum(item => item.CashAmount?.MinorUnits ?? 0);
        long entries = data.FinancialEntries
            .Where(item => item.Type != FinancialEntryType.OtherIncome && InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits);
        long recurringExpenses = data.UnofficialExpenses
            .Where(item => item.AppliesInMonth(month))
            .Sum(item => item.MonthlyAmount.MinorUnits);
        long annualProvisions = data.Obligations
            .Where(item => item.Recurrence == RecurrenceFrequency.Annual
                && item.DueDate.Year == month.Year)
            .Sum(item => ProratedAnnualAmount(item.ExpectedAmount.MinorUnits, month.Month));
        long annualPaymentAdjustments = data.ObligationPayments
            .Where(item => InMonth(item.Date))
            .Select(item => new
            {
                Payment = item,
                Obligation = data.Obligations.Single(obligation =>
                    obligation.Id == item.ObligationId),
            })
            .Where(item => item.Obligation.Recurrence == RecurrenceFrequency.Annual)
            .Sum(item => item.Payment.Amount.MinorUnits - item.Obligation.ExpectedAmount.MinorUnits);
        long obligationPayments = data.ObligationPayments
            .Where(item => InMonth(item.Date)
                && data.Obligations.Any(obligation =>
                    obligation.Id == item.ObligationId
                    && obligation.Recurrence != RecurrenceFrequency.Annual)
                && !reservedActualIds.Contains((FinancialCommitmentSource.Obligation, item.ObligationId)))
            .Sum(item => item.Amount.MinorUnits);
        long maintenance = data.MaintenanceRecords
            .Where(item => item.CompletedDate.HasValue
                && InMonth(item.CompletedDate.Value)
                && !reservedActualIds.Contains((FinancialCommitmentSource.Maintenance, item.Id)))
            .Sum(item => item.ActualCost?.MinorUnits ?? 0);
        long loanPayments = data.LoanPayments
            .Where(item => InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits);
        long unreservedLoanPayments = data.LoanPayments
            .Where(item => InMonth(item.Date)
                && !reservedActualIds.Contains((
                    FinancialCommitmentSource.LoanInstallment,
                    item.InstallmentId ?? item.LoanId)))
            .Sum(item => item.Amount.MinorUnits);
        long paidOutflows = checked(
            purchases + entries + recurringExpenses + annualProvisions + annualPaymentAdjustments
            + obligationPayments + maintenance + unreservedLoanPayments);

        long financing = data.CollaboratorContributions
            .Where(item => InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits)
            + data.Loans
                .Where(item => InMonth(item.StartDate))
                .Sum(item => item.InitialBalance.MinorUnits);
        long priorUncovered = candidates
            .Where(item => !item.IsExcluded
                && item.DueDate < month.FirstDay
                && !activeReserves.Any(reserve =>
                    reserve.SourceType == item.SourceType && reserve.SourceId == item.SourceId))
            .Sum(item => item.ExpectedMinorUnits);
        newReserves -= priorUncovered;

        long distributable = checked(
            collectedIncome - paidOutflows - newReserves - reserveAdjustments - priorUncovered);
        long positiveBase = Math.Max(0, distributable);
        long fund = ApplyPercentage(positiveBase, globalPercentage.BasisPoints);
        long retained = positiveBase - fund;
        long breakEven = checked(
            paidOutflows + newReserves + Math.Max(0, reserveAdjustments) + priorUncovered);
        long shortfall = Math.Max(0, -distributable);

        return new FinancialMonthSnapshot(
            month,
            collectedIncome,
            accountsReceivable,
            paidOutflows,
            accountsPayable,
            newReserves,
            carriedReserves,
            reserveAdjustments,
            loanPayments,
            financing,
            priorUncovered,
            distributable,
            breakEven,
            shortfall,
            fund,
            retained,
            globalPercentage.BasisPoints,
            candidates);
    }

    private static IReadOnlyList<FinancialCommitmentCandidate> BuildCandidates(
        AdministrationData data,
        YearMonth month,
        DateOnly end)
    {
        var result = new List<FinancialCommitmentCandidate>();
        foreach (Obligation obligation in data.Obligations.Where(item =>
                     item.DueDate <= end && item.Recurrence != RecurrenceFrequency.Annual))
        {
            ObligationPayment[] payments = data.ObligationPayments
                .Where(item => item.ObligationId == obligation.Id && item.Date <= end)
                .ToArray();
            long paid = payments.Sum(item => item.Amount.MinorUnits);
            long pending = ObligationOutstandingAt(obligation, data.ObligationPayments, end);
            if (pending == 0)
            {
                continue;
            }

            result.Add(Candidate(
                FinancialCommitmentSource.Obligation,
                obligation.Id,
                "Obligación",
                obligation.Name,
                obligation.DueDate,
                pending,
                paid,
                obligation.DueDate < month.FirstDay ? "Vencida" : "Pendiente",
                data,
                month));
        }

        foreach (var maintenance in data.MaintenanceRecords.Where(item =>
                     item.ScheduledDate <= end
                     && (!item.CompletedDate.HasValue || item.CompletedDate.Value > end)))
        {
            long expected = maintenance.EstimatedCost?.MinorUnits ?? 0;
            result.Add(Candidate(
                FinancialCommitmentSource.Maintenance,
                maintenance.Id,
                "Mantenimiento",
                $"{maintenance.Asset}: {maintenance.MaintenanceType}",
                maintenance.ScheduledDate,
                expected,
                0,
                maintenance.ScheduledDate < month.FirstDay ? "Vencido" : "Pendiente",
                data,
                month));
        }

        foreach (MonthlyPurchaseItem item in data.MonthlyPurchaseItems.Where(item =>
                     MonthlyPurchasePendingAt(item, data, end)))
        {
            result.Add(Candidate(
                FinancialCommitmentSource.MonthlyPurchase,
                item.Id,
                "Lista mensual de compra",
                item.Name,
                item.Month.LastDay,
                item.ExpectedTotalMinorUnits,
                0,
                item.Month.LastDay < month.FirstDay ? "Vencida" : "Pendiente",
                data,
                month));
        }

        Guid[] scheduledLoanIds = data.LoanInstallments.Select(item => item.LoanId).Distinct().ToArray();
        foreach (LoanInstallment installment in data.LoanInstallments.Where(item =>
                     item.DueDate <= end
                     && data.LoanPayments.All(payment =>
                         payment.InstallmentId != item.Id || payment.Date > end)))
        {
            Loan? loan = data.Loans.SingleOrDefault(item => item.Id == installment.LoanId);
            result.Add(Candidate(
                FinancialCommitmentSource.LoanInstallment,
                installment.Id,
                "Préstamo",
                loan?.Name ?? "Préstamo eliminado",
                installment.DueDate,
                installment.Amount.MinorUnits,
                0,
                installment.DueDate < month.FirstDay ? "Vencida" : "Pendiente",
                data,
                month));
        }

        foreach (Loan loan in data.Loans.Where(item =>
                     !scheduledLoanIds.Contains(item.Id)
                     && item.StartDate <= end
                     && LoanOutstandingAt(item, data.LoanPayments, end) > 0
                     && item.NextDueDate <= end))
        {
            long outstanding = LoanOutstandingAt(loan, data.LoanPayments, end);
            long amount = Math.Min(loan.UsualInstallment.MinorUnits, outstanding);
            result.Add(Candidate(
                FinancialCommitmentSource.LoanInstallment,
                loan.Id,
                "Préstamo",
                loan.Name,
                loan.NextDueDate,
                amount,
                0,
                loan.NextDueDate < month.FirstDay ? "Vencida" : "Pendiente",
                data,
                month));
        }

        return result.OrderBy(item => item.DueDate).ThenBy(item => item.Name).ToArray();
    }

    private static long ProratedAnnualAmount(long annualMinorUnits, int month)
    {
        long regular = annualMinorUnits / 12;
        return month == 12
            ? checked(annualMinorUnits - regular * 11)
            : regular;
    }

    private static FinancialCommitmentCandidate Candidate(
        FinancialCommitmentSource type,
        Guid sourceId,
        string origin,
        string name,
        DateOnly dueDate,
        long expected,
        long actual,
        string status,
        AdministrationData data,
        YearMonth month)
    {
        FinancialCloseExclusion? exclusion = data.FinancialCloseExclusions
            .SingleOrDefault(item =>
                item.Month == month && item.SourceType == type && item.SourceId == sourceId);
        return new(
            type,
            sourceId,
            origin,
            name,
            dueDate,
            expected,
            actual,
            status,
            exclusion is not null,
            exclusion?.Reason);
    }

    private static Dictionary<(FinancialCommitmentSource, Guid), long> ActualsBySource(
        AdministrationData data,
        YearMonth month)
    {
        bool InMonth(DateOnly date) => YearMonth.From(date) == month;
        var result = new Dictionary<(FinancialCommitmentSource, Guid), long>();
        foreach (var group in data.ObligationPayments
                     .Where(item => InMonth(item.Date))
                     .GroupBy(item => item.ObligationId))
        {
            result[(FinancialCommitmentSource.Obligation, group.Key)] =
                group.Sum(item => item.Amount.MinorUnits);
        }

        foreach (var item in data.MaintenanceRecords.Where(item =>
                     item.CompletedDate.HasValue && InMonth(item.CompletedDate.Value)))
        {
            result[(FinancialCommitmentSource.Maintenance, item.Id)] =
                item.ActualCost?.MinorUnits ?? 0;
        }

        foreach (var item in data.MonthlyPurchaseItems.Where(item => item.PurchaseMovementId.HasValue))
        {
            InventoryMovement? movement = data.InventoryMovements.SingleOrDefault(value =>
                value.Id == item.PurchaseMovementId && InMonth(value.Date));
            if (movement is not null)
            {
                result[(FinancialCommitmentSource.MonthlyPurchase, item.Id)] =
                    movement.CashAmount?.MinorUnits ?? 0;
            }
        }

        foreach (var group in data.LoanPayments
                     .Where(item => InMonth(item.Date))
                     .GroupBy(item => item.InstallmentId ?? item.LoanId))
        {
            result[(FinancialCommitmentSource.LoanInstallment, group.Key)] =
                group.Sum(item => item.Amount.MinorUnits);
        }

        return result;
    }

    private static long CalculateLocalUseDebt(AdministrationData data, DateOnly end) =>
        data.LocalUsePeople.Sum(person =>
        {
            long charges = data.WeeklyCharges
                .Where(item => item.PersonId == person.Id && item.DueDate <= end)
                .Sum(item => item.Amount.MinorUnits)
                + data.DailyCharges
                    .Where(item => item.PersonId == person.Id && item.ChargeDate <= end)
                    .Sum(item => item.Amount.MinorUnits);
            long payments = data.LocalUsePayments
                .Where(item => item.PersonId == person.Id && item.PaymentDate <= end)
                .Sum(item => item.Amount.MinorUnits);
            return Math.Max(0, charges - payments);
        });

    private static long ObligationOutstandingAt(
        Obligation obligation,
        IEnumerable<ObligationPayment> allPayments,
        DateOnly cutoff)
    {
        ObligationPayment[] payments = allPayments
            .Where(item => item.ObligationId == obligation.Id)
            .ToArray();
        long paidAtCutoff = payments
            .Where(item => item.Date <= cutoff)
            .Sum(item => item.Amount.MinorUnits);
        bool settledAtCutoff = obligation.IsSettled
            && payments.Length > 0
            && payments.Max(item => item.Date) <= cutoff;
        return settledAtCutoff
            ? 0
            : Math.Max(0, obligation.ExpectedAmount.MinorUnits - paidAtCutoff);
    }

    private static bool MonthlyPurchasePendingAt(
        MonthlyPurchaseItem item,
        AdministrationData data,
        DateOnly cutoff)
    {
        if (item.Month.LastDay > cutoff)
        {
            return false;
        }

        if (!item.PurchaseMovementId.HasValue)
        {
            return true;
        }

        InventoryMovement? movement = data.InventoryMovements
            .SingleOrDefault(value => value.Id == item.PurchaseMovementId.Value);
        return movement is null || movement.Date > cutoff;
    }

    private static long LoanOutstandingAt(
        Loan loan,
        IEnumerable<LoanPayment> payments,
        DateOnly cutoff)
    {
        long paid = payments
            .Where(item => item.LoanId == loan.Id && item.Date <= cutoff)
            .Sum(item => item.Amount.MinorUnits);
        return Math.Max(0, loan.ExpectedTotal.MinorUnits - paid);
    }

    private static bool ReserveIsActiveAt(FinancialReserve reserve, DateOnly cutoff) =>
        reserve.DueDate <= cutoff
        && (!reserve.SettledDate.HasValue || reserve.SettledDate.Value > cutoff);

    private static long ApplyPercentage(long minorUnits, int basisPoints) => checked((long)decimal.Round(
        minorUnits * (basisPoints / 10_000m),
        0,
        MidpointRounding.AwayFromZero));
}
