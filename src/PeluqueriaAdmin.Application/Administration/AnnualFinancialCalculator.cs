using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
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
        if (year is < 2000 or > 2200) throw new ArgumentOutOfRangeException(nameof(year));
        var months = new List<AnnualMonthFinancial>(12);
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

        FinancialMonthSnapshot? latestOpenSnapshot = null;
        foreach (int monthNumber in Enumerable.Range(1, 12))
        {
            YearMonth month = new(year, monthNumber);
            MonthlyClose? close = data.MonthlyCloses
                .Where(item => item.Month == month && item.IsConfirmed)
                .OrderByDescending(item => item.ClosedUtc)
                .FirstOrDefault();
            FinancialMonthSnapshot? snapshot = close?.ToFinancialSnapshot();
            if (snapshot is null && annual is null && month.FirstDay <= today)
            {
                snapshot = FinancialMonthCalculator.Calculate(data, collaboratorPercentage, month);
                latestOpenSnapshot = snapshot;
            }

            long outflows = snapshot is null ? 0 : checked(
                snapshot.PaidOutflowsMinorUnits
                + snapshot.NewReservesMinorUnits
                + snapshot.ReserveAdjustmentsMinorUnits
                + snapshot.PriorUncoveredCommitmentsMinorUnits);
            months.Add(new AnnualMonthFinancial(
                month,
                snapshot?.CollectedOperatingIncomeMinorUnits ?? 0,
                outflows,
                snapshot?.DistributableResultMinorUnits ?? 0,
                close is not null));
        }

        long income = annual?.Income ?? months.Sum(item => item.IncomeMinorUnits);
        long outflow = annual?.Outflow ?? months.Sum(item => item.OutflowMinorUnits);
        long result = annual?.Result ?? months.Sum(item => item.ResultMinorUnits);
        long receivable = annual?.Receivable ?? latestOpenSnapshot?.AccountsReceivableMinorUnits
            ?? CalculateAccountsReceivable(data, year, today);
        long payable = annual?.Payable ?? CalculateAccountsPayable(data, year, today);
        long reserves = annual?.Reserves ?? data.FinancialReserves
            .Where(item => !item.IsConsumed && item.DueDate.Year <= year)
            .Sum(item => item.ReservedAmount.MinorUnits);
        long loans = annual?.Loans ?? data.Loans.Sum(item => item.PendingBalance.MinorUnits);
        long fund = annual?.Fund ?? months.Select(month =>
        {
            MonthlyClose? close = data.MonthlyCloses
                .Where(item => item.Month == month.Month && item.IsConfirmed)
                .OrderByDescending(item => item.ClosedUtc)
                .FirstOrDefault();
            return close?.FundMinorUnits
                ?? (month.Month.FirstDay <= today
                    ? FinancialMonthCalculator.Calculate(data, collaboratorPercentage, month.Month)
                        .CollaboratorFundMinorUnits
                    : 0);
        }).Sum();
        long surplus = annual?.Surplus ?? Math.Max(result, 0);
        long deficit = annual?.Deficit ?? Math.Max(-result, 0);
        long projected = annual?.Projected ?? checked(surplus + receivable - payable - reserves - loans - deficit);

        return new AnnualFinancialReport(
            year,
            months,
            BuildCommitments(data, year, today),
            income,
            outflow,
            result,
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

    private static long CalculateAccountsPayable(AdministrationData data, int year, DateOnly today)
    {
        DateOnly cutoff = new(year, 12, 31);
        long obligations = data.Obligations.Where(item => item.DueDate <= cutoff)
            .Sum(item => item.OutstandingAmount(data.ObligationPayments).MinorUnits);
        long maintenance = data.MaintenanceRecords
            .Where(item => !item.CompletedDate.HasValue && item.ScheduledDate <= cutoff)
            .Sum(item => item.EstimatedCost?.MinorUnits ?? 0);
        long monthlyPurchases = data.MonthlyPurchaseItems
            .Where(item => MonthlyPurchaseCommitmentPolicy.IsPending(item, data, cutoff))
            .Sum(item => item.ExpectedTotalMinorUnits);
        return checked(obligations + maintenance + monthlyPurchases);
    }

    private static long CalculateAccountsReceivable(AdministrationData data, int year, DateOnly today)
    {
        DateOnly cutoff = new(year, 12, 31);
        long charges = data.WeeklyCharges.Where(item => item.DueDate <= cutoff)
            .Sum(item => item.Amount.MinorUnits);
        long payments = data.LocalUsePayments.Where(item => item.PaymentDate <= cutoff)
            .Sum(item => item.Amount.MinorUnits);
        return Math.Max(0, charges - payments);
    }

    private static IReadOnlyList<AnnualPendingCommitment> BuildCommitments(
        AdministrationData data,
        int year,
        DateOnly today)
    {
        DateOnly cutoff = new(year, 12, 31);
        var result = new List<AnnualPendingCommitment>();
        foreach (var obligation in data.Obligations.Where(item => item.DueDate <= cutoff))
        {
            long pending = obligation.OutstandingAmount(data.ObligationPayments).MinorUnits;
            if (pending > 0) result.Add(new(
                obligation.DueDate, obligation.Name, "Obligación", pending,
                obligation.Description ?? string.Empty, obligation.DueDate < today ? "Vencida" : "Pendiente"));
        }
        foreach (var maintenance in data.MaintenanceRecords.Where(item =>
                     !item.CompletedDate.HasValue && item.ScheduledDate <= cutoff))
        {
            long pending = maintenance.EstimatedCost?.MinorUnits ?? 0;
            if (pending > 0) result.Add(new(
                maintenance.ScheduledDate,
                $"{maintenance.Asset}: {maintenance.MaintenanceType}",
                "Mantenimiento",
                pending,
                maintenance.Description ?? string.Empty,
                maintenance.ScheduledDate < today ? "Vencido" : "Pendiente"));
        }
        foreach (var item in data.MonthlyPurchaseItems.Where(item =>
                     MonthlyPurchaseCommitmentPolicy.IsPending(item, data, cutoff)))
        {
            result.Add(new(
                item.Month.LastDay,
                item.Name,
                "Compra mensual",
                item.ExpectedTotalMinorUnits,
                item.Description ?? string.Empty,
                item.Month.LastDay < today ? "Vencida" : "Pendiente"));
        }
        foreach (var installment in data.LoanInstallments.Where(item => item.DueDate <= cutoff
                     && data.LoanPayments.All(payment => payment.InstallmentId != item.Id)))
        {
            string loan = data.Loans.SingleOrDefault(item => item.Id == installment.LoanId)?.Name
                ?? "Préstamo eliminado";
            result.Add(new(
                installment.DueDate, loan, "Cuota de préstamo", installment.Amount.MinorUnits,
                installment.Description ?? string.Empty, installment.DueDate < today ? "Vencida" : "Pendiente"));
        }
        return result.OrderBy(item => item.DueDate).ThenBy(item => item.Name).ToArray();
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
