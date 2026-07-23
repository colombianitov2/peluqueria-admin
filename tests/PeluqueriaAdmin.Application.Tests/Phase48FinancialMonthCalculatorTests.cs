using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.Activity;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Tests;

public sealed class Phase48FinancialMonthCalculatorTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RequiredScenario_ExcludesWorkerDebtAndProducesExactlyFiftyFourDollarFund()
    {
        var month = new YearMonth(2026, 7);
        var worker = LocalUsePerson.Create("Trabajador", new DateOnly(2026, 7, 1), null, UtcNow);
        WeeklyRate rate = WeeklyRate.Create(worker.EntryDate, Money.FromDecimal(120m), UtcNow);
        WeeklyCharge charge = Assert.Single(WeeklyChargeCalculator.Generate(worker, [], [rate], new DateOnly(2026, 7, 8), UtcNow));
        FinancialEntry income = FinancialEntry.CreateIncome(new DateOnly(2026, 7, 18), "Ingresos cobrados", Money.FromDecimal(1000m), UtcNow);
        FinancialEntry expense = FinancialEntry.CreateExpense(new DateOnly(2026, 7, 18), "Egresos pagados",
            ExpenseCategory.Other, Money.FromDecimal(500m), UtcNow);
        Obligation electricity = Obligation.Create("Electricidad", ObligationType.Service, new DateOnly(2026, 7, 25),
            Money.FromDecimal(100m), RecurrenceFrequency.None, UtcNow);
        MaintenanceRecord maintenance = MaintenanceRecord.Schedule("Aire", "Mantenimiento vencido",
            new DateOnly(2026, 7, 10), Money.FromDecimal(80m), MaintenanceFrequency.Once, null, null, UtcNow);
        Loan loan = Loan.Create("Préstamo", Money.FromDecimal(500m), Money.FromDecimal(50m),
            new DateOnly(2026, 6, 1), LoanFrequency.Monthly, 10, new DateOnly(2026, 7, 20), UtcNow);
        AdministrationData data = EmptyData() with
        {
            LocalUsePeople = [worker],
            WeeklyRates = [rate],
            WeeklyCharges = [charge],
            FinancialEntries = [income, expense],
            Obligations = [electricity],
            MaintenanceRecords = [maintenance],
            Loans = [loan],
        };

        var result = FinancialMonthCalculator.Calculate(data, Percentage.FromPercent(20m), month);

        Assert.Equal(100_000, result.CollectedOperatingIncomeMinorUnits);
        Assert.Equal(12_000, result.AccountsReceivableMinorUnits);
        Assert.Equal(50_000, result.PaidOutflowsMinorUnits);
        Assert.Equal(23_000, result.NewReservesMinorUnits);
        Assert.Equal(27_000, result.DistributableResultMinorUnits);
        Assert.Equal(5_400, result.CollaboratorFundMinorUnits);
    }

    [Fact]
    public void ReservedElectricityPaidForOneHundredTen_OnlyTenAffectsLaterPeriod()
    {
        var obligation = Obligation.Create("Electricidad", ObligationType.Service, new DateOnly(2026, 7, 25),
            Money.FromDecimal(100m), RecurrenceFrequency.None, UtcNow);
        var reserve = FinancialReserve.Create(new YearMonth(2026, 7), FinancialCommitmentSource.Obligation,
            obligation.Id, obligation.Name, obligation.DueDate, Money.FromDecimal(100m), UtcNow);
        var payment = ObligationPayment.Create(obligation.Id, new DateOnly(2026, 8, 2),
            Money.FromDecimal(110m), UtcNow);
        var income = FinancialEntry.CreateIncome(new DateOnly(2026, 8, 2), "Ingreso", Money.FromDecimal(100m), UtcNow);
        AdministrationData data = EmptyData() with
        {
            Obligations = [obligation],
            ObligationPayments = [payment],
            FinancialReserves = [reserve],
            FinancialEntries = [income],
        };

        var result = FinancialMonthCalculator.Calculate(data, Percentage.FromPercent(0m), new YearMonth(2026, 8));

        Assert.Equal(1_000, result.ReserveAdjustmentsMinorUnits);
        Assert.Equal(0, result.PaidOutflowsMinorUnits);
        Assert.Equal(9_000, result.DistributableResultMinorUnits);
    }

    [Fact]
    public void NegativeResult_ProducesZeroFundAndVisibleLocalShortfall()
    {
        FinancialEntry expense = FinancialEntry.CreateExpense(new DateOnly(2026, 7, 3), "Gasto",
            ExpenseCategory.Other, Money.FromDecimal(75m), UtcNow);

        FinancialMonthSnapshot result = FinancialMonthCalculator.Calculate(
            EmptyData() with { FinancialEntries = [expense] }, Percentage.FromPercent(20m), new YearMonth(2026, 7));

        Assert.Equal(-7_500, result.DistributableResultMinorUnits);
        Assert.Equal(7_500, result.ShortfallMinorUnits);
        Assert.Equal(0, result.CollaboratorFundMinorUnits);
        Assert.Equal(0, result.RetainedLocalMinorUnits);
    }

    [Fact]
    public void Maintenance_FutureDoesNotAffectMonth_ButOverdueCreatesCommitment()
    {
        MaintenanceRecord overdue = MaintenanceRecord.Schedule("Aire", "Vencido", new DateOnly(2026, 7, 10),
            Money.FromDecimal(80m), MaintenanceFrequency.Once, null, null, UtcNow);
        MaintenanceRecord future = MaintenanceRecord.Schedule("Nevera", "Futuro", new DateOnly(2026, 8, 10),
            Money.FromDecimal(90m), MaintenanceFrequency.Once, null, null, UtcNow);

        FinancialMonthSnapshot result = FinancialMonthCalculator.Calculate(
            EmptyData() with { MaintenanceRecords = [overdue, future] }, Percentage.FromPercent(20m), new YearMonth(2026, 7));

        FinancialCommitmentCandidate candidate = Assert.Single(result.Candidates);
        Assert.Equal(overdue.Id, candidate.SourceId);
        Assert.Equal(8_000, result.NewReservesMinorUnits);
    }

    [Fact]
    public void MaintenanceWithoutEstimate_RemainsVisibleAsUnresolvedCloseCandidate()
    {
        MaintenanceRecord unresolved = MaintenanceRecord.Schedule("Aire", "Sin estimación",
            new DateOnly(2026, 7, 10), null, MaintenanceFrequency.Once, null, null, UtcNow);

        FinancialMonthSnapshot result = FinancialMonthCalculator.Calculate(
            EmptyData() with { MaintenanceRecords = [unresolved] }, Percentage.FromPercent(20m), new YearMonth(2026, 7));

        FinancialCommitmentCandidate candidate = Assert.Single(result.Candidates);
        Assert.Equal(0, candidate.ExpectedMinorUnits);
        Assert.Equal(FinancialCommitmentSource.Maintenance, candidate.SourceType);
    }

    [Fact]
    public void LoanReceived_IsFinancingOnly_AndInstallmentReducesDistributableResult()
    {
        Loan loan = Loan.Create("Crédito", Money.FromDecimal(500m), Money.FromDecimal(50m),
            new DateOnly(2026, 7, 1), LoanFrequency.Monthly, 10, new DateOnly(2026, 7, 20), UtcNow);
        FinancialEntry income = FinancialEntry.CreateIncome(new DateOnly(2026, 7, 2), "Ingreso", Money.FromDecimal(100m), UtcNow);

        FinancialMonthSnapshot result = FinancialMonthCalculator.Calculate(
            EmptyData() with { Loans = [loan], FinancialEntries = [income] }, Percentage.FromPercent(20m), new YearMonth(2026, 7));

        Assert.Equal(50_000, result.FinancingReceivedMinorUnits);
        Assert.Equal(10_000, result.CollectedOperatingIncomeMinorUnits);
        Assert.Equal(5_000, result.NewReservesMinorUnits);
        Assert.Equal(5_000, result.DistributableResultMinorUnits);
    }

    [Fact]
    public void MonthlyPurchase_UsesQuantityTimesCost_AndRemainsPendingAfterStartingMonth()
    {
        Product product = Product.Create("Champú", ProductCategory.Cleaning, "unidad", UtcNow);
        MonthlyPurchaseItem item = MonthlyPurchaseItem.Create(product.Id, new YearMonth(2026, 7), 3,
            Money.FromDecimal(12.50m), true, true, UtcNow);
        AdministrationData data = EmptyData() with { Products = [product], MonthlyPurchaseItems = [item] };

        FinancialMonthSnapshot july = FinancialMonthCalculator.Calculate(data, Percentage.FromPercent(0m), new YearMonth(2026, 7));
        FinancialMonthSnapshot august = FinancialMonthCalculator.Calculate(data, Percentage.FromPercent(0m), new YearMonth(2026, 8));

        Assert.Equal(3_750, july.NewReservesMinorUnits);
        Assert.Equal(3_750, august.NewReservesMinorUnits);
        Assert.Equal(FinancialCommitmentSource.MonthlyPurchase, Assert.Single(august.Candidates).SourceType);
    }

    [Fact]
    public void FutureObligationPayment_DoesNotEraseEarlierMonthAccountPayable()
    {
        Obligation obligation = Obligation.Create("Servicio", ObligationType.Service, new DateOnly(2026, 7, 20),
            Money.FromDecimal(100m), RecurrenceFrequency.None, UtcNow);
        ObligationPayment futurePayment = ObligationPayment.Create(obligation.Id, new DateOnly(2026, 8, 2),
            Money.FromDecimal(100m), UtcNow);

        FinancialMonthSnapshot result = FinancialMonthCalculator.Calculate(
            EmptyData() with { Obligations = [obligation], ObligationPayments = [futurePayment] },
            Percentage.FromPercent(0m), new YearMonth(2026, 7));

        Assert.Equal(10_000, result.AccountsPayableMinorUnits);
        Assert.Equal(10_000, result.NewReservesMinorUnits);
    }

    [Fact]
    public void ReservedAmountHigherThanActual_ReleasesOnlyTheDifference()
    {
        Obligation obligation = Obligation.Create("Electricidad", ObligationType.Service, new DateOnly(2026, 7, 25),
            Money.FromDecimal(100m), RecurrenceFrequency.None, UtcNow);
        FinancialReserve reserve = FinancialReserve.Create(new YearMonth(2026, 7), FinancialCommitmentSource.Obligation,
            obligation.Id, obligation.Name, obligation.DueDate, Money.FromDecimal(100m), UtcNow);
        ObligationPayment payment = ObligationPayment.Create(obligation.Id, new DateOnly(2026, 8, 2),
            Money.FromDecimal(80m), UtcNow);

        FinancialMonthSnapshot result = FinancialMonthCalculator.Calculate(
            EmptyData() with { Obligations = [obligation], ObligationPayments = [payment], FinancialReserves = [reserve] },
            Percentage.FromPercent(0m), new YearMonth(2026, 8));

        Assert.Equal(-2_000, result.ReserveAdjustmentsMinorUnits);
        Assert.Equal(2_000, result.DistributableResultMinorUnits);
    }

    [Fact]
    public void ChangingMonth_DoesNotMixCollectedOperatingMovements()
    {
        FinancialEntry july = FinancialEntry.CreateIncome(new DateOnly(2026, 7, 31), "Julio", Money.FromDecimal(100m), UtcNow);
        FinancialEntry august = FinancialEntry.CreateIncome(new DateOnly(2026, 8, 1), "Agosto", Money.FromDecimal(20m), UtcNow);
        AdministrationData data = EmptyData() with { FinancialEntries = [july, august] };

        Assert.Equal(10_000, FinancialMonthCalculator.Calculate(data, Percentage.FromPercent(20m), new YearMonth(2026, 7)).CollectedOperatingIncomeMinorUnits);
        Assert.Equal(2_000, FinancialMonthCalculator.Calculate(data, Percentage.FromPercent(20m), new YearMonth(2026, 8)).CollectedOperatingIncomeMinorUnits);
    }

    private static AdministrationData EmptyData() => new(
        [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], [], []);
}
