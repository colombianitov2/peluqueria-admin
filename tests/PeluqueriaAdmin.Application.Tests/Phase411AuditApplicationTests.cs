using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Tests;

public sealed class Phase411AuditApplicationTests
{
    private static readonly DateTime Utc = new(2026, 7, 24, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AccountsReceivable_DoesNotOffsetOneWorkersDebtWithAnotherWorkersCredit()
    {
        DateOnly entry = new(2026, 1, 1);
        DateOnly cutoff = new(2026, 1, 10);
        WeeklyRate rate = WeeklyRate.Create(
            entry,
            Money.FromDecimal(12m),
            Utc);
        LocalUsePerson debtor = LocalUsePerson.Create(
            "Deudor",
            entry,
            null,
            Utc);
        LocalUsePerson prepaid = LocalUsePerson.Create(
            "Anticipado",
            entry,
            null,
            Utc);
        IReadOnlyList<WeeklyCharge> debtorCharges =
            WeeklyChargeCalculator.Generate(
                debtor,
                [],
                [rate],
                cutoff,
                Utc);
        IReadOnlyList<WeeklyCharge> prepaidCharges =
            WeeklyChargeCalculator.Generate(
                prepaid,
                [],
                [rate],
                cutoff,
                Utc);
        LocalUsePayment advance = LocalUsePayment.Create(
            prepaid.Id,
            cutoff,
            Money.FromDecimal(24m),
            Utc);

        Assert.Equal([514L, 1_200L],
            debtorCharges.Select(item => item.Amount.MinorUnits));
        Assert.Equal([514L, 1_200L],
            prepaidCharges.Select(item => item.Amount.MinorUnits));

        FinancialMonthSnapshot result = FinancialMonthCalculator.Calculate(
            EmptyData() with
            {
                LocalUsePeople = [debtor, prepaid],
                WeeklyCharges = debtorCharges
                    .Concat(prepaidCharges)
                    .ToArray(),
                LocalUsePayments = [advance],
                WeeklyRates = [rate],
            },
            Percentage.FromPercent(20m),
            new YearMonth(2026, 1));

        Assert.Equal(
            1_714,
            result.AccountsReceivableMinorUnits);
    }

    [Fact]
    public void ExcludedObligation_RemainsInAccountsPayableButDoesNotCreateAReserve()
    {
        var month = new YearMonth(2026, 7);
        Obligation obligation = Obligation.Create(
            "Electricidad",
            ObligationType.Service,
            new DateOnly(2026, 7, 20),
            Money.FromDecimal(100m),
            RecurrenceFrequency.None,
            Utc);
        FinancialCloseExclusion exclusion = FinancialCloseExclusion.Create(
            month,
            FinancialCommitmentSource.Obligation,
            obligation.Id,
            "Se pagará posteriormente",
            Utc);

        FinancialMonthSnapshot result = FinancialMonthCalculator.Calculate(
            EmptyData() with
            {
                Obligations = [obligation],
                FinancialCloseExclusions = [exclusion],
            },
            Percentage.FromPercent(20m),
            month);

        Assert.Equal(10_000, result.AccountsPayableMinorUnits);
        Assert.Equal(0, result.NewReservesMinorUnits);
    }

    [Fact]
    public void AnnualBalance_CountsOneOverdueObligationOnlyOnce()
    {
        Obligation obligation = Obligation.Create(
            "Impuesto anual",
            ObligationType.Tax,
            new DateOnly(2026, 1, 15),
            Money.FromDecimal(100m),
            RecurrenceFrequency.None,
            Utc);

        AnnualFinancialReport result = AnnualFinancialCalculator.Calculate(
            EmptyData() with { Obligations = [obligation] },
            Percentage.FromPercent(20m),
            2026,
            new DateOnly(2026, 12, 31));

        Assert.Equal(10_000, result.OutflowMinorUnits);
        Assert.Equal(-10_000, result.ResultMinorUnits);
        Assert.Equal(10_000, result.AccountsPayableMinorUnits);
        Assert.Equal(-10_000, result.Months[0].ResultMinorUnits);
        Assert.All(result.Months.Skip(1), month => Assert.Equal(0, month.ResultMinorUnits));
    }

    [Fact]
    public void HistoricalAnnualLoanBalance_IgnoresPaymentsMadeInALaterYear()
    {
        LoanPlan plan = LoanCalculator.AgreedFinalAmount(
            "Préstamo histórico",
            Money.FromDecimal(100m),
            Money.FromDecimal(150m),
            5,
            new DateOnly(2025, 1, 31),
            Utc);
        LoanPayment first = LoanPayment.CreateScheduled(
            plan.Loan.Id,
            plan.Installments[0].Id,
            new DateOnly(2025, 2, 1),
            plan.Installments[0].Amount,
            Utc);
        LoanPayment later = LoanPayment.CreateScheduled(
            plan.Loan.Id,
            plan.Installments[1].Id,
            new DateOnly(2026, 1, 1),
            plan.Installments[1].Amount,
            Utc.AddYears(1));

        AnnualFinancialReport result = AnnualFinancialCalculator.Calculate(
            EmptyData() with
            {
                Loans = [plan.Loan],
                LoanInstallments = plan.Installments,
                LoanPayments = [first, later],
            },
            Percentage.FromPercent(0m),
            2025,
            new DateOnly(2026, 7, 24));

        Assert.Equal(12_000, result.PendingLoansMinorUnits);
    }

    [Fact]
    public void NonConsecutiveMonthlyCloses_UseEachLatestMonthAndKeepOpenMonthLive()
    {
        MonthlyClose january = MonthlyClose.Create(
            Snapshot(new YearMonth(2026, 1), 10_000, 6_000), Utc);
        MonthlyClose march = MonthlyClose.Create(
            Snapshot(new YearMonth(2026, 3), 30_000, 20_000), Utc.AddHours(1));
        FinancialEntry february = FinancialEntry.CreateIncome(
            new DateOnly(2026, 2, 10), "Ingreso febrero", Money.FromDecimal(50m), Utc);

        AnnualFinancialReport result = AnnualFinancialCalculator.Calculate(
            EmptyData() with
            {
                MonthlyCloses = [january, march],
                FinancialEntries = [february],
            },
            Percentage.FromPercent(0m),
            2026,
            new DateOnly(2026, 3, 31));

        Assert.Equal(10_000, result.Months[0].IncomeMinorUnits);
        Assert.Equal(5_000, result.Months[1].IncomeMinorUnits);
        Assert.Equal(30_000, result.Months[2].IncomeMinorUnits);
        Assert.Equal(45_000, result.IncomeMinorUnits);
    }

    private static FinancialMonthSnapshot Snapshot(YearMonth month, long income, long result) => new(
        month,
        income,
        0,
        income - result,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        result,
        Math.Max(0, income - result),
        Math.Max(0, -result),
        0,
        Math.Max(0, result),
        0,
        []);

    private static AdministrationData EmptyData() => new(
        [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], []);
}
