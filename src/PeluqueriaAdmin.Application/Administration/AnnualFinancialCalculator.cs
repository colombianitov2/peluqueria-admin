using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Administration;

public sealed record AnnualMonthFinancial(
    YearMonth Month,
    long IncomeMinorUnits,
    long OutflowMinorUnits,
    long ResultMinorUnits,
    bool IsClosed);

public sealed record AnnualPendingCommitment(
    DateOnly DueDate,
    string Name,
    string Type,
    long AmountMinorUnits,
    string Description,
    string Status);

public sealed record AnnualFinancialReport(
    int Year,
    IReadOnlyList<AnnualMonthFinancial> Months,
    IReadOnlyList<AnnualPendingCommitment> Commitments,
    long IncomeMinorUnits,
    long OutflowMinorUnits,
    long ResultMinorUnits,
    long AccountsReceivableMinorUnits,
    long AccountsPayableMinorUnits,
    long PendingReservesMinorUnits,
    long PendingLoansMinorUnits,
    long CollaboratorFundMinorUnits,
    long SurplusMinorUnits,
    long DeficitMinorUnits,
    long ProjectedNextYearBalanceMinorUnits,
    bool IsClosed);

public static class AnnualFinancialCalculator
{
    public static AnnualFinancialReport Calculate(
        AdministrationData data,
        Percentage collaboratorPercentage,
        int year,
        DateOnly today)
    {
        if (year is < 2000 or > 2200)
        {
            throw new ArgumentOutOfRangeException(nameof(year));
        }

        AnnualCloseSnapshot? annual = data.AnnualCloses
            .Where(item => item.Year == year)
            .Select(item => new AnnualCloseSnapshot(
                item.IncomeMinorUnits,
                item.PaidOutflowsMinorUnits,
                item.ResultMinorUnits,
                item.AccountsReceivableMinorUnits,
                item.AccountsPayableMinorUnits,
                item.PendingReservesMinorUnits,
                item.PendingLoansMinorUnits,
                item.CollaboratorFundMinorUnits,
                item.SurplusMinorUnits,
                item.DeficitMinorUnits,
                item.ProjectedNextYearBalanceMinorUnits))
            .SingleOrDefault();
        DateOnly cutoff = EffectiveCutoff(year, today);
        var months = new List<AnnualMonthFinancial>(12);
        var snapshots = new Dictionary<YearMonth, FinancialMonthSnapshot>();

        foreach (int monthNumber in Enumerable.Range(1, 12))
        {
            var month = new YearMonth(year, monthNumber);
            MonthlyClose? close = data.MonthlyCloses
                .Where(item => item.Month == month && item.IsConfirmed)
                .OrderByDescending(item => item.ClosedUtc)
                .FirstOrDefault();
            FinancialMonthSnapshot? snapshot = close?.ToFinancialSnapshot();
            if (snapshot is null && annual is null && month.FirstDay <= cutoff)
            {
                snapshot = FinancialMonthCalculator.Calculate(data, collaboratorPercentage, month);
            }

            if (snapshot is not null)
            {
                snapshots[month] = snapshot;
            }

            long income = snapshot?.CollectedOperatingIncomeMinorUnits ?? 0;
            long outflow = snapshot is null
                ? 0
                : checked(
                    snapshot.PaidOutflowsMinorUnits
                    + snapshot.NewReservesMinorUnits
                    + snapshot.ReserveAdjustmentsMinorUnits);
            long result = checked(income - outflow);
            months.Add(new AnnualMonthFinancial(month, income, outflow, result, close is not null));
        }

        long totalIncome = annual?.Income ?? months.Sum(item => item.IncomeMinorUnits);
        long totalOutflow = annual?.Outflow ?? months.Sum(item => item.OutflowMinorUnits);
        long totalResult = annual?.Result ?? months.Sum(item => item.ResultMinorUnits);
        long receivable = annual?.Receivable ?? CalculateAccountsReceivable(data, cutoff);
        long payable = annual?.Payable ?? CalculateAccountsPayable(data, cutoff);
        long reserves = annual?.Reserves ?? data.FinancialReserves
            .Where(item => item.DueDate <= cutoff
                && (!item.SettledDate.HasValue || item.SettledDate.Value > cutoff))
            .Sum(item => item.ReservedAmount.MinorUnits);
        long loans = annual?.Loans ?? CalculatePendingLoans(data, cutoff);
        long fund = annual?.Fund ?? snapshots.Values.Sum(item => item.CollaboratorFundMinorUnits);
        long surplus = annual?.Surplus ?? Math.Max(totalResult, 0);
        long deficit = annual?.Deficit ?? Math.Max(-totalResult, 0);
        long projected = annual?.Projected
            ?? checked(surplus + receivable - payable - reserves - loans - deficit);

        return new AnnualFinancialReport(
            year,
            months,
            BuildCommitments(data, cutoff, today),
            totalIncome,
            totalOutflow,
            totalResult,
            receivable,
            payable,
            reserves,
            loans,
            fund,
            surplus,
            deficit,
            projected,
            annual is not null);
    }

    private static DateOnly EffectiveCutoff(int year, DateOnly today)
    {
        DateOnly yearEnd = new(year, 12, 31);
        return today.Year == year && today < yearEnd ? today : yearEnd;
    }

    private static long CalculateAccountsPayable(AdministrationData data, DateOnly cutoff)
    {
        long obligations = data.Obligations
            .Where(item => item.DueDate <= cutoff)
            .Sum(item => ObligationOutstandingAt(item, data.ObligationPayments, cutoff));
        long maintenance = data.MaintenanceRecords
            .Where(item => item.ScheduledDate <= cutoff
                && (!item.CompletedDate.HasValue || item.CompletedDate.Value > cutoff))
            .Sum(item => item.EstimatedCost?.MinorUnits ?? 0);
        long monthlyPurchases = data.MonthlyPurchaseItems
            .Where(item => MonthlyPurchasePendingAt(item, data, cutoff))
            .Sum(item => item.ExpectedTotalMinorUnits);
        return checked(obligations + maintenance + monthlyPurchases);
    }

    private static long CalculateAccountsReceivable(AdministrationData data, DateOnly cutoff) =>
        data.LocalUsePeople.Sum(person =>
        {
            long charges = data.WeeklyCharges
                .Where(item => item.PersonId == person.Id && item.DueDate <= cutoff)
                .Sum(item => item.Amount.MinorUnits)
                + data.DailyCharges
                    .Where(item => item.PersonId == person.Id && item.ChargeDate <= cutoff)
                    .Sum(item => item.Amount.MinorUnits);
            long payments = data.LocalUsePayments
                .Where(item => item.PersonId == person.Id && item.PaymentDate <= cutoff)
                .Sum(item => item.Amount.MinorUnits);
            return Math.Max(0, charges - payments);
        });

    private static long CalculatePendingLoans(AdministrationData data, DateOnly cutoff) =>
        data.Loans
            .Where(item => item.StartDate <= cutoff)
            .Sum(loan =>
            {
                long paid = data.LoanPayments
                    .Where(item => item.LoanId == loan.Id && item.Date <= cutoff)
                    .Sum(item => item.Amount.MinorUnits);
                return Math.Max(0, loan.ExpectedTotal.MinorUnits - paid);
            });

    private static IReadOnlyList<AnnualPendingCommitment> BuildCommitments(
        AdministrationData data,
        DateOnly cutoff,
        DateOnly today)
    {
        var result = new List<AnnualPendingCommitment>();
        foreach (Obligation obligation in data.Obligations.Where(item => item.DueDate <= cutoff))
        {
            long pending = ObligationOutstandingAt(obligation, data.ObligationPayments, cutoff);
            if (pending > 0)
            {
                result.Add(new AnnualPendingCommitment(
                    obligation.DueDate,
                    obligation.Name,
                    "Obligación",
                    pending,
                    obligation.Description ?? string.Empty,
                    obligation.DueDate < today ? "Vencida" : "Pendiente"));
            }
        }

        foreach (var maintenance in data.MaintenanceRecords.Where(item =>
                     item.ScheduledDate <= cutoff
                     && (!item.CompletedDate.HasValue || item.CompletedDate.Value > cutoff)))
        {
            long pending = maintenance.EstimatedCost?.MinorUnits ?? 0;
            if (pending > 0)
            {
                result.Add(new AnnualPendingCommitment(
                    maintenance.ScheduledDate,
                    $"{maintenance.Asset}: {maintenance.MaintenanceType}",
                    "Mantenimiento",
                    pending,
                    maintenance.Description ?? string.Empty,
                    maintenance.ScheduledDate < today ? "Vencido" : "Pendiente"));
            }
        }

        foreach (MonthlyPurchaseItem item in data.MonthlyPurchaseItems.Where(item =>
                     MonthlyPurchasePendingAt(item, data, cutoff)))
        {
            result.Add(new AnnualPendingCommitment(
                item.Month.LastDay,
                item.Name,
                "Compra mensual",
                item.ExpectedTotalMinorUnits,
                item.Description ?? string.Empty,
                item.Month.LastDay < today ? "Vencida" : "Pendiente"));
        }

        foreach (LoanInstallment installment in data.LoanInstallments.Where(item =>
                     item.DueDate <= cutoff
                     && data.LoanPayments.All(payment =>
                         payment.InstallmentId != item.Id || payment.Date > cutoff)))
        {
            string loanName = data.Loans.SingleOrDefault(item => item.Id == installment.LoanId)?.Name
                ?? "Préstamo eliminado";
            result.Add(new AnnualPendingCommitment(
                installment.DueDate,
                loanName,
                "Cuota de préstamo",
                installment.Amount.MinorUnits,
                installment.Description ?? string.Empty,
                installment.DueDate < today ? "Vencida" : "Pendiente"));
        }

        return result.OrderBy(item => item.DueDate).ThenBy(item => item.Name).ToArray();
    }

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

    private sealed record AnnualCloseSnapshot(
        long Income,
        long Outflow,
        long Result,
        long Receivable,
        long Payable,
        long Reserves,
        long Loans,
        long Fund,
        long Surplus,
        long Deficit,
        long Projected);
}
