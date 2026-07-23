using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class Phase49DomainTests
{
    private static readonly DateTime Utc = new(2026, 7, 23, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Phase49_04_NextChargeIsTheNextSaturday()
    {
        LocalUsePerson person = Worker();
        WeeklyRate rate = Rate();
        WorkerAccountBalance account = WeeklyChargeCalculator.CalculateAccount(
            person, [], [], [rate], new DateOnly(2026, 7, 23));
        Assert.Equal(new DateOnly(2026, 7, 25), account.NextChargeDate);
    }

    [Fact]
    public void Phase49_05_AgreedPlanExposesFirstUncoveredMonthlyPayment()
    {
        LocalUsePerson person = Worker();
        WeeklyRate rate = Rate();
        LocalUsePayment advance = LocalUsePayment.Create(
            person.Id, new DateOnly(2026, 7, 23), Money.FromDecimal(24m), Utc);
        WorkerAccountBalance account = WeeklyChargeCalculator.CalculateAccount(
            person, [], [advance], [rate], new DateOnly(2026, 7, 23));
        Assert.Equal(new DateOnly(2026, 8, 8), account.NextRequiredPaymentDate);
    }

    [Fact]
    public void Phase49_06_ScheduledPaymentConsumesTheExactOutstandingAmount()
    {
        LocalUsePerson person = Worker();
        WeeklyRate rate = Rate();
        LocalUsePayment advance = LocalUsePayment.Create(
            person.Id, new DateOnly(2026, 7, 23), Money.FromDecimal(24m), Utc);
        WeeklyCharge charge = Assert.Single(WeeklyChargeCalculator.Generate(
            person, [], [rate], new DateOnly(2026, 7, 25), Utc));
        WorkerAccountBalance account = WeeklyChargeCalculator.CalculateAccount(
            person, [charge], [advance], [rate], new DateOnly(2026, 7, 25));
        Assert.Equal(1_200, account.Credit.MinorUnits);
    }

    [Fact]
    public void Phase49_08_DeletedContributionEventKeepsItsAmount()
    {
        CollaboratorContribution contribution = Contribution(25m);
        CollaboratorContributionEvent deleted = CollaboratorContributionEvent.Deleted(contribution, Utc.AddMinutes(1));
        Assert.Equal(CollaboratorContributionEventType.Deleted, deleted.EventType);
        Assert.Equal(2_500, deleted.Amount.MinorUnits);
    }

    [Fact]
    public void Phase49_09_DeletedContributionEventKeepsChronologyAndDescription()
    {
        CollaboratorContribution contribution = Contribution(25m);
        CollaboratorContributionEvent deleted = CollaboratorContributionEvent.Deleted(contribution, Utc.AddMinutes(1));
        Assert.Equal(Utc.AddMinutes(1), deleted.OccurredUtc);
        Assert.Equal("Capital inicial", deleted.Description);
    }

    [Fact]
    public void Phase49_10_EditedContributionEventKeepsPreviousAndNewValues()
    {
        CollaboratorContribution contribution = Contribution(25m);
        contribution.Update(new DateOnly(2026, 7, 24), Money.FromDecimal(40m), "Capital corregido", Utc.AddMinutes(1));
        CollaboratorContributionEvent edited = CollaboratorContributionEvent.Edited(
            contribution, new DateOnly(2026, 7, 23), Money.FromDecimal(25m), "Capital inicial", Utc.AddMinutes(1));
        Assert.Equal(2_500, edited.PreviousAmount?.MinorUnits);
        Assert.Equal(4_000, edited.Amount.MinorUnits);
    }

    [Fact]
    public void Phase49_13_PlannedProductCanExistWithoutInventoryProduct()
    {
        MonthlyPurchaseItem item = PlannedProduct();
        Assert.Null(item.ProductId);
        Assert.Null(item.PurchaseMovementId);
    }

    [Fact]
    public void Phase49_14_PlannedProductAcceptsAFreeNameAndCategory()
    {
        MonthlyPurchaseItem item = PlannedProduct();
        Assert.Equal("Tinte futuro", item.Name);
        Assert.Equal(ProductCategory.ProductForSale, item.Category);
    }

    [Fact]
    public void Phase49_19_MonthlyBalanceInterestUsesFixedPaymentAmortization()
    {
        LoanPlan plan = LoanCalculator.MonthlyBalanceInterest("Banco", Money.FromDecimal(1_000m),
            2m, 12, new DateOnly(2026, 8, 15), Utc);
        Assert.Equal(12, plan.Installments.Count);
        Assert.All(plan.Installments.Take(plan.Installments.Count - 1),
            item => Assert.Equal(plan.Loan.UsualInstallment, item.Amount));
        Assert.Equal(0, plan.Installments[^1].PrincipalBalanceAfter.MinorUnits);
    }

    [Fact]
    public void Phase49_20_AgreedFinalAmountUsesOnlyTheConfiguredTotal()
    {
        LoanPlan plan = LoanCalculator.AgreedFinalAmount("Persona", Money.FromDecimal(100m),
            Money.FromDecimal(150m), 5, new DateOnly(2026, 8, 1), Utc);
        Assert.Equal(15_000, plan.Loan.ExpectedTotal.MinorUnits);
        Assert.Equal(LoanCalculationMethod.AgreedFinalAmount, plan.Loan.CalculationMethod);
    }

    [Fact]
    public void Phase49_21_OneHundredToOneHundredFiftyProducesFiveThirtyDollarInstallments()
    {
        LoanPlan plan = LoanCalculator.AgreedFinalAmount("Persona", Money.FromDecimal(100m),
            Money.FromDecimal(150m), 5, new DateOnly(2026, 8, 1), Utc);
        Assert.All(plan.Installments, item => Assert.Equal(3_000, item.Amount.MinorUnits));
    }

    [Fact]
    public void Phase49_22_OneHundredToOneHundredFiftyHasExactlyFiftyPercentTotalInterest()
    {
        LoanPlan plan = LoanCalculator.AgreedFinalAmount("Persona", Money.FromDecimal(100m),
            Money.FromDecimal(150m), 5, new DateOnly(2026, 8, 1), Utc);
        Assert.Equal(5_000, plan.Loan.TotalInterest.MinorUnits);
        Assert.Equal(50m, plan.Loan.TotalInterest.ToDecimal() / plan.Loan.InitialBalance.ToDecimal() * 100m);
    }

    [Fact]
    public void Phase49_23_AllInstallmentsSumExactlyToExpectedTotal()
    {
        LoanPlan plan = LoanCalculator.MonthlyBalanceInterest("Banco", Money.FromDecimal(987.65m),
            1.37m, 17, new DateOnly(2026, 8, 31), Utc);
        Assert.Equal(plan.Loan.ExpectedTotal.MinorUnits, plan.Installments.Sum(item => item.Amount.MinorUnits));
    }

    [Fact]
    public void Phase49_24_LastInstallmentAbsorbsResidualCents()
    {
        LoanPlan plan = LoanCalculator.AgreedFinalAmount("Persona", Money.FromDecimal(100m),
            Money.FromDecimal(100.01m), 3, new DateOnly(2026, 8, 1), Utc);
        Assert.Equal([3_333L, 3_333L, 3_335L], plan.Installments.Select(item => item.Amount.MinorUnits));
    }

    [Fact]
    public void Phase49_25_InstallmentCalendarAdvancesByCalendarMonth()
    {
        LoanPlan plan = LoanCalculator.AgreedFinalAmount("Persona", Money.FromDecimal(100m),
            Money.FromDecimal(150m), 3, new DateOnly(2026, 1, 31), Utc);
        Assert.Equal(
            [new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31)],
            plan.Installments.Select(item => item.DueDate));
    }

    private static CollaboratorContribution Contribution(decimal amount) =>
        CollaboratorContribution.Create(Guid.NewGuid(), new DateOnly(2026, 7, 23),
            Money.FromDecimal(amount), "Capital inicial", Utc);

    private static MonthlyPurchaseItem PlannedProduct() =>
        MonthlyPurchaseItem.Create("Tinte futuro", ProductCategory.ProductForSale,
            new YearMonth(2026, 8), 2, Money.FromDecimal(15m), true, true, Utc, "Plan independiente");

    private static LocalUsePerson Worker() =>
        LocalUsePerson.Create("Trabajador", new DateOnly(2026, 7, 20), null, Utc);

    private static WeeklyRate Rate() =>
        WeeklyRate.Create(new DateOnly(2026, 7, 20), Money.FromDecimal(12m), Utc);
}
