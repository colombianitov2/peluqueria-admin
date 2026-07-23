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

        long localUseCollected = data.LocalUsePayments.Where(item => InMonth(item.PaymentDate))
            .Sum(item => item.Amount.MinorUnits);
        long sales = data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Sale && InMonth(item.Date))
            .Sum(item => item.CashAmount?.MinorUnits ?? 0);
        long otherIncome = data.FinancialEntries.Where(item => item.Type == FinancialEntryType.OtherIncome && InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits);
        long collectedIncome = checked(localUseCollected + sales + otherIncome);

        long accountsReceivable = CalculateLocalUseDebt(data, end);
        var candidates = BuildCandidates(data, month, end);
        long accountsPayable = candidates.Where(item => !item.IsExcluded).Sum(item => item.ExpectedMinorUnits);

        FinancialReserve[] activeReserves = data.FinancialReserves.Where(item => !item.IsConsumed).ToArray();
        long carriedReserves = activeReserves.Where(item => item.Month.Year < month.Year
                || item.Month.Year == month.Year && item.Month.Month < month.Month)
            .Sum(item => item.ReservedAmount.MinorUnits);
        long newReserves = candidates.Where(item => !item.IsExcluded
                && !activeReserves.Any(reserve => reserve.SourceType == item.SourceType && reserve.SourceId == item.SourceId))
            .Sum(item => item.ExpectedMinorUnits);

        var actualsBySource = ActualsBySource(data, month);
        long reserveAdjustments = 0;
        var reservedActualIds = new HashSet<(FinancialCommitmentSource, Guid)>();
        foreach (FinancialReserve reserve in data.FinancialReserves)
        {
            if (!actualsBySource.TryGetValue((reserve.SourceType, reserve.SourceId), out long actual)) continue;
            reserveAdjustments = checked(reserveAdjustments + actual - reserve.ReservedAmount.MinorUnits);
            reservedActualIds.Add((reserve.SourceType, reserve.SourceId));
        }

        long purchases = data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Purchase && InMonth(item.Date)
                && !data.MonthlyPurchaseItems.Any(plan => plan.PurchaseMovementId == item.Id
                    && reservedActualIds.Contains((FinancialCommitmentSource.MonthlyPurchase, plan.Id))))
            .Sum(item => item.CashAmount?.MinorUnits ?? 0);
        long entries = data.FinancialEntries.Where(item => item.Type != FinancialEntryType.OtherIncome && InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits);
        long obligationPayments = data.ObligationPayments.Where(item => InMonth(item.Date)
                && !reservedActualIds.Contains((FinancialCommitmentSource.Obligation, item.ObligationId)))
            .Sum(item => item.Amount.MinorUnits);
        long maintenance = data.MaintenanceRecords.Where(item => item.CompletedDate.HasValue && InMonth(item.CompletedDate.Value)
                && !reservedActualIds.Contains((FinancialCommitmentSource.Maintenance, item.Id)))
            .Sum(item => item.ActualCost?.MinorUnits ?? 0);
        long loanPayments = data.LoanPayments.Where(item => InMonth(item.Date)).Sum(item => item.Amount.MinorUnits);
        long unreservedLoanPayments = data.LoanPayments.Where(item => InMonth(item.Date)
                && !reservedActualIds.Contains((FinancialCommitmentSource.LoanInstallment, item.LoanId)))
            .Sum(item => item.Amount.MinorUnits);
        long paidOutflows = checked(purchases + entries + obligationPayments + maintenance + unreservedLoanPayments);

        long financing = data.CollaboratorContributions.Where(item => InMonth(item.Date)).Sum(item => item.Amount.MinorUnits)
            + data.Loans.Where(item => InMonth(item.StartDate)).Sum(item => item.InitialBalance.MinorUnits);
        long priorUncovered = candidates.Where(item => !item.IsExcluded && item.DueDate < month.FirstDay
                && !activeReserves.Any(reserve => reserve.SourceType == item.SourceType && reserve.SourceId == item.SourceId))
            .Sum(item => item.ExpectedMinorUnits);
        // Los vencidos sin reserva se arrastran; no vuelven a contarse también como reserva nueva.
        newReserves -= priorUncovered;

        long distributable = checked(collectedIncome - paidOutflows - newReserves - reserveAdjustments - priorUncovered);
        long positiveBase = Math.Max(0, distributable);
        long fund = ApplyPercentage(positiveBase, globalPercentage.BasisPoints);
        long retained = positiveBase - fund;
        long breakEven = checked(paidOutflows + newReserves + Math.Max(0, reserveAdjustments) + priorUncovered);
        long shortfall = Math.Max(0, -distributable);

        return new FinancialMonthSnapshot(month, collectedIncome, accountsReceivable, paidOutflows,
            accountsPayable, newReserves, carriedReserves, reserveAdjustments, loanPayments, financing,
            priorUncovered, distributable, breakEven, shortfall, fund, retained,
            globalPercentage.BasisPoints, candidates);
    }

    private static IReadOnlyList<FinancialCommitmentCandidate> BuildCandidates(
        AdministrationData data, YearMonth month, DateOnly end)
    {
        var result = new List<FinancialCommitmentCandidate>();
        foreach (Obligation obligation in data.Obligations.Where(item => item.DueDate <= end))
        {
            long paid = data.ObligationPayments.Where(item => item.ObligationId == obligation.Id && item.Date <= end)
                .Sum(item => item.Amount.MinorUnits);
            long pending = Math.Max(0, obligation.ExpectedAmount.MinorUnits - paid);
            if (pending == 0) continue;
            result.Add(Candidate(FinancialCommitmentSource.Obligation, obligation.Id, "Obligación",
                obligation.Name, obligation.DueDate, pending, paid, obligation.DueDate < month.FirstDay ? "Vencida" : "Pendiente", data, month));
        }

        foreach (var maintenance in data.MaintenanceRecords.Where(item => !item.CompletedDate.HasValue && item.ScheduledDate <= end))
        {
            long expected = maintenance.EstimatedCost?.MinorUnits ?? 0;
            result.Add(Candidate(FinancialCommitmentSource.Maintenance, maintenance.Id, "Mantenimiento",
                $"{maintenance.Asset}: {maintenance.MaintenanceType}", maintenance.ScheduledDate,
                expected, 0, maintenance.ScheduledDate < month.FirstDay ? "Vencido" : "Pendiente", data, month));
        }

        var stock = data.InventoryMovements.GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.QuantityDelta));
        foreach (MonthlyPurchaseItem item in data.MonthlyPurchaseItems.Where(item =>
                     (item.Month.Year < month.Year || item.Month.Year == month.Year && item.Month.Month <= month.Month)
                     && item.IsActive && !item.PurchaseMovementId.HasValue))
        {
            if (!item.ReserveWhenOutOfStock || stock.GetValueOrDefault(item.ProductId) > 0) continue;
            string name = data.Products.SingleOrDefault(product => product.Id == item.ProductId)?.Name ?? "Producto";
            result.Add(Candidate(FinancialCommitmentSource.MonthlyPurchase, item.Id, "Lista mensual de compra",
                name, month.LastDay, item.ExpectedTotalMinorUnits, 0, "Pendiente", data, month));
        }

        foreach (Loan loan in data.Loans.Where(item => !item.IsPaid && item.NextDueDate <= end))
        {
            long amount = Math.Min(loan.UsualInstallment.MinorUnits, loan.PendingBalance.MinorUnits);
            result.Add(Candidate(FinancialCommitmentSource.LoanInstallment, loan.Id, "Préstamo",
                loan.Name, loan.NextDueDate, amount, 0, loan.NextDueDate < month.FirstDay ? "Vencida" : "Pendiente", data, month));
        }
        return result.OrderBy(item => item.DueDate).ThenBy(item => item.Name).ToArray();
    }

    private static FinancialCommitmentCandidate Candidate(FinancialCommitmentSource type, Guid sourceId,
        string origin, string name, DateOnly dueDate, long expected, long actual, string status,
        AdministrationData data, YearMonth month)
    {
        FinancialCloseExclusion? exclusion = data.FinancialCloseExclusions
            .SingleOrDefault(item => item.Month == month && item.SourceType == type && item.SourceId == sourceId);
        return new(type, sourceId, origin, name, dueDate, expected, actual, status,
            exclusion is not null, exclusion?.Reason);
    }

    private static Dictionary<(FinancialCommitmentSource, Guid), long> ActualsBySource(AdministrationData data, YearMonth month)
    {
        bool InMonth(DateOnly date) => YearMonth.From(date) == month;
        var result = new Dictionary<(FinancialCommitmentSource, Guid), long>();
        foreach (var group in data.ObligationPayments.Where(item => InMonth(item.Date)).GroupBy(item => item.ObligationId))
            result[(FinancialCommitmentSource.Obligation, group.Key)] = group.Sum(item => item.Amount.MinorUnits);
        foreach (var item in data.MaintenanceRecords.Where(item => item.CompletedDate.HasValue && InMonth(item.CompletedDate.Value)))
            result[(FinancialCommitmentSource.Maintenance, item.Id)] = item.ActualCost?.MinorUnits ?? 0;
        foreach (var item in data.MonthlyPurchaseItems.Where(item => item.PurchaseMovementId.HasValue))
        {
            InventoryMovement? movement = data.InventoryMovements.SingleOrDefault(value => value.Id == item.PurchaseMovementId && InMonth(value.Date));
            if (movement is not null) result[(FinancialCommitmentSource.MonthlyPurchase, item.Id)] = movement.CashAmount?.MinorUnits ?? 0;
        }
        foreach (var group in data.LoanPayments.Where(item => InMonth(item.Date)).GroupBy(item => item.LoanId))
            result[(FinancialCommitmentSource.LoanInstallment, group.Key)] = group.Sum(item => item.Amount.MinorUnits);
        return result;
    }

    private static long CalculateLocalUseDebt(AdministrationData data, DateOnly end)
    {
        long charges = data.WeeklyCharges.Where(item => item.PeriodEnd <= end).Sum(item => item.Amount.MinorUnits);
        long payments = data.LocalUsePayments.Where(item => item.PaymentDate <= end).Sum(item => item.Amount.MinorUnits);
        return Math.Max(0, charges - payments);
    }

    private static long ApplyPercentage(long minorUnits, int basisPoints) => checked((long)decimal.Round(
        minorUnits * (basisPoints / 10_000m), 0, MidpointRounding.AwayFromZero));
}
