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

    [Fact]
    public void Phase50B_RecurringExpenseAffectsResultBreakEvenAndCollaboratorFundOnce()
    {
        UnofficialExpense expense = UnofficialExpense.Create(
            "Administración",
            Money.FromDecimal(100m),
            new DateOnly(2026, 7, 10),
            "Gasto mensual real",
            Utc);
        FinancialEntry income = FinancialEntry.CreateIncome(
            new DateOnly(2026, 7, 20),
            "Ingreso",
            Money.FromDecimal(150m),
            Utc);

        FinancialMonthSnapshot result = FinancialMonthCalculator.Calculate(
            EmptyData() with
            {
                UnofficialExpenses = [expense],
                FinancialEntries = [income],
            },
            Percentage.FromPercent(20m),
            new YearMonth(2026, 7));

        Assert.Equal(10_000, result.PaidOutflowsMinorUnits);
        Assert.Equal(10_000, result.BreakEvenMinorUnits);
        Assert.Equal(5_000, result.DistributableResultMinorUnits);
        Assert.Equal(1_000, result.CollaboratorFundMinorUnits);
        Assert.Equal(4_000, result.RetainedLocalMinorUnits);
    }

    [Fact]
    public void Phase50B_AnnualLiveMonthsIncludeRecurringExpenses()
    {
        UnofficialExpense expense = UnofficialExpense.Create(
            "Administración",
            Money.FromDecimal(100m),
            new DateOnly(2026, 7, 10),
            null,
            Utc);

        AnnualFinancialReport report = AnnualFinancialCalculator.Calculate(
            EmptyData() with { UnofficialExpenses = [expense] },
            Percentage.FromPercent(0m),
            2026,
            new DateOnly(2026, 8, 31));

        Assert.Equal(10_000, report.Months[6].OutflowMinorUnits);
        Assert.Equal(10_000, report.Months[7].OutflowMinorUnits);
        Assert.Equal(20_000, report.OutflowMinorUnits);
        Assert.Equal(-20_000, report.ResultMinorUnits);
    }

    [Fact]
    public void Phase50B_AnnualObligationIsProratedAcrossTwelveMonthsWithoutDoubleCountingPayment()
    {
        Obligation annual = Obligation.Create(
            "Seguro anual",
            ObligationType.Service,
            new DateOnly(2026, 12, 15),
            Money.FromDecimal(1_200m),
            RecurrenceFrequency.Annual,
            Utc);
        ObligationPayment payment = ObligationPayment.Create(
            annual.Id,
            new DateOnly(2026, 12, 15),
            Money.FromDecimal(1_200m),
            Utc);
        AdministrationData data = EmptyData() with
        {
            Obligations = [annual],
            ObligationPayments = [payment],
        };

        FinancialMonthSnapshot january = FinancialMonthCalculator.Calculate(
            data, Percentage.FromPercent(0m), new YearMonth(2026, 1));
        FinancialMonthSnapshot december = FinancialMonthCalculator.Calculate(
            data, Percentage.FromPercent(0m), new YearMonth(2026, 12));
        AnnualFinancialReport report = AnnualFinancialCalculator.Calculate(
            data, Percentage.FromPercent(0m), 2026, new DateOnly(2026, 12, 31));

        Assert.Equal(10_000, january.PaidOutflowsMinorUnits);
        Assert.Equal(10_000, december.PaidOutflowsMinorUnits);
        Assert.Equal(120_000, report.OutflowMinorUnits);
        Assert.Equal(-120_000, report.ResultMinorUnits);
    }

    [Fact]
    public void Phase50C_MonthlyCashUsesRequestedCategoriesAndCarriesBalanceExactlyOnce()
    {
        FinancialEntry julyIncome = FinancialEntry.CreateIncome(
            new DateOnly(2026, 7, 10),
            "Ingreso julio",
            Money.FromDecimal(300m),
            Utc);
        UnofficialExpense recurring = UnofficialExpense.Create(
            "Administración",
            Money.FromDecimal(100m),
            new DateOnly(2026, 7, 1),
            null,
            Utc);
        AdministrationData data = EmptyData() with
        {
            FinancialEntries = [julyIncome],
            UnofficialExpenses = [recurring],
        };

        MonthlyCashBreakdown july = AdministrationReports.MonthlyCash(
            data, Percentage.FromPercent(20m), new YearMonth(2026, 7));
        MonthlyCashBreakdown august = AdministrationReports.MonthlyCash(
            data, Percentage.FromPercent(20m), new YearMonth(2026, 8));
        MonthlyCashBreakdown september = AdministrationReports.MonthlyCash(
            data, Percentage.FromPercent(20m), new YearMonth(2026, 9));

        Assert.Equal(30_000, july.TotalIncomeMinorUnits);
        Assert.Equal(10_000, july.RecurringExpensesMinorUnits);
        Assert.Equal(10_000, july.TotalSpentMinorUnits);
        Assert.Equal(20_000, july.DifferenceMinorUnits);
        Assert.Equal(20_000, july.CarryOutMinorUnits);
        Assert.Equal(20_000, august.CarryInMinorUnits);
        Assert.Equal(10_000, august.CarryOutMinorUnits);
        Assert.Equal(10_000, september.CarryInMinorUnits);
        Assert.Equal(0, september.CarryOutMinorUnits);
    }

    [Fact]
    public void Phase50C_AnnualCommitmentIsShownAsOneTwelfthInItsVisibleCategory()
    {
        Obligation annual = Obligation.Create(
            "Seguro anual",
            ObligationType.Service,
            new DateOnly(2026, 12, 15),
            Money.FromDecimal(1_200m),
            RecurrenceFrequency.Annual,
            Utc);
        AdministrationData data = EmptyData() with { Obligations = [annual] };

        MonthlyCashBreakdown january = AdministrationReports.MonthlyCash(
            data, Percentage.FromPercent(0m), new YearMonth(2026, 1));
        MonthlyCashBreakdown december = AdministrationReports.MonthlyCash(
            data, Percentage.FromPercent(0m), new YearMonth(2026, 12));

        Assert.Equal(10_000, january.ServicesMinorUnits);
        Assert.Equal(10_000, december.ServicesMinorUnits);
        Assert.Equal(10_000, january.BreakEvenMinorUnits);
        Assert.Equal(10_000, december.BreakEvenMinorUnits);
    }

    [Fact]
    public void Phase50C_AnnualPaymentOnlyAppliesItsDifferenceAndNeverDuplicatesTheCommitment()
    {
        Obligation annual = Obligation.Create(
            "Seguro anual",
            ObligationType.Service,
            new DateOnly(2026, 12, 15),
            Money.FromDecimal(1_200m),
            RecurrenceFrequency.Annual,
            Utc);
        ObligationPayment payment = ObligationPayment.Create(
            annual.Id,
            new DateOnly(2026, 12, 15),
            Money.FromDecimal(1_260m),
            Utc);
        AdministrationData data = EmptyData() with
        {
            Obligations = [annual],
            ObligationPayments = [payment],
        };

        MonthlyCashBreakdown january = AdministrationReports.MonthlyCash(
            data, Percentage.FromPercent(0m), new YearMonth(2026, 1));
        MonthlyCashBreakdown december = AdministrationReports.MonthlyCash(
            data, Percentage.FromPercent(0m), new YearMonth(2026, 12));
        long annualTotal = Enumerable.Range(1, 12)
            .Select(month => AdministrationReports.MonthlyCash(
                data, Percentage.FromPercent(0m), new YearMonth(2026, month)))
            .Sum(month => month.ServicesMinorUnits);

        Assert.Equal(10_000, january.ServicesMinorUnits);
        Assert.Equal(16_000, december.ServicesMinorUnits);
        Assert.Equal(126_000, annualTotal);
        Assert.Equal(16_000, december.BreakEvenMinorUnits);
    }

    [Fact]
    public void Phase50D_LoanOutflowsAreIncludedOnceAndFinancingStaysSeparateFromOperatingIncome()
    {
        FinancialEntry income = FinancialEntry.CreateIncome(
            new DateOnly(2026, 7, 10),
            "Ingresos operativos",
            Money.FromDecimal(1_692.86m),
            Utc);
        FinancialEntry expense = FinancialEntry.CreateExpense(
            new DateOnly(2026, 7, 10),
            "Gastos",
            ExpenseCategory.Other,
            Money.FromDecimal(370m),
            Utc);
        UnofficialExpense recurring = UnofficialExpense.Create(
            "Extraoficial",
            Money.FromDecimal(45m),
            new DateOnly(2026, 5, 1),
            null,
            Utc);
        Obligation service = Obligation.Create(
            "Servicio",
            ObligationType.Service,
            new DateOnly(2026, 7, 20),
            Money.FromDecimal(75m),
            RecurrenceFrequency.None,
            Utc);
        Loan loan = Loan.Create(
            "Préstamo",
            Money.FromDecimal(100m),
            Money.FromDecimal(30m),
            new DateOnly(2026, 5, 1),
            LoanFrequency.Monthly,
            5,
            new DateOnly(2026, 5, 31),
            Utc);
        Collaborator collaborator = Collaborator.Create("Socio", new DateOnly(2026, 5, 1), null, Utc);
        CollaboratorContribution contribution = CollaboratorContribution.Create(
            collaborator.Id,
            new DateOnly(2026, 5, 1),
            Money.FromDecimal(350m),
            null,
            Utc);
        AdministrationData data = EmptyData() with
        {
            FinancialEntries = [income, expense],
            UnofficialExpenses = [recurring],
            Obligations = [service],
            Loans = [loan],
            Collaborators = [collaborator],
            CollaboratorContributions = [contribution],
        };

        MonthlyCashBreakdown july = AdministrationReports.MonthlyCash(
            data, Percentage.FromPercent(0m), new YearMonth(2026, 7));
        MonthlyCashBreakdown may = AdministrationReports.MonthlyCash(
            data, Percentage.FromPercent(0m), new YearMonth(2026, 5));
        MonthlyCashBreakdown june = AdministrationReports.MonthlyCash(
            data, Percentage.FromPercent(0m), new YearMonth(2026, 6));
        MonthlyCashBreakdown[] months = Enumerable.Range(1, 7)
            .Select(month => AdministrationReports.MonthlyCash(
                data, Percentage.FromPercent(0m), new YearMonth(2026, month)))
            .ToArray();

        Assert.Equal(
            169_286,
            july.LocalUseIncomeMinorUnits + july.SalesIncomeMinorUnits
            + july.OtherIncomeMinorUnits + july.OtherRealIncomeMinorUnits);
        Assert.Equal(0, may.LocalUseIncomeMinorUnits + may.SalesIncomeMinorUnits
            + may.OtherIncomeMinorUnits + may.OtherRealIncomeMinorUnits);
        Assert.Equal(35_000, may.CollaboratorContributionsMinorUnits);
        Assert.Equal(0, june.LocalUseIncomeMinorUnits + june.SalesIncomeMinorUnits
            + june.OtherIncomeMinorUnits + june.OtherRealIncomeMinorUnits);
        Assert.Equal(10_000, may.FinancingReceivedMinorUnits);
        Assert.Equal(3_000, july.LoanPaymentsMinorUnits);
        Assert.Equal(
            july.InventoryMinorUnits + july.GeneralExpensesMinorUnits
            + july.RecurringExpensesMinorUnits + july.UnexpectedExpensesMinorUnits
            + july.ServicesMinorUnits + july.TaxesMinorUnits
            + july.OtherObligationsMinorUnits + july.LoanPaymentsMinorUnits
            + july.CreditPaymentsMinorUnits + july.MaintenanceMinorUnits
            + july.CollaboratorPaymentsMinorUnits + july.OtherOutflowsMinorUnits,
            july.TotalSpentMinorUnits);
        Assert.Equal(july.TotalSpentMinorUnits, july.BreakEvenMinorUnits);
        Assert.Equal(
            july.TotalIncomeMinorUnits - july.TotalSpentMinorUnits
            + july.CollaboratorContributionsMinorUnits + july.FinancingReceivedMinorUnits,
            july.CarryOutMinorUnits);
        Assert.Equal(9_000, months.Sum(month => month.LoanPaymentsMinorUnits));
        Assert.Equal(
            months.Sum(month => month.TotalSpentMinorUnits),
            months.Sum(month => month.BreakEvenMinorUnits));
    }

    [Fact]
    public void Phase50D_DocumentedMonthlyAndAnnualExamplesRemainExact()
    {
        const long monthlyAvailable = 120_500 + 4_286 + 45_000;
        const long monthlySpent = 4_500 + 7_500 + 6_000;
        Assert.Equal(169_786, monthlyAvailable);
        Assert.Equal(18_000, monthlySpent);
        Assert.Equal(151_786, monthlyAvailable - monthlySpent);

        const long annualOperating = 169_286;
        const long annualSpent = 71_500;
        const long financing = 35_000 + 10_000;
        Assert.Equal(97_786, annualOperating - annualSpent);
        Assert.Equal(142_786, annualOperating - annualSpent + financing);
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
