using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class Phase48FinancialDomainTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ZeroResult_FreezesOneZeroPaymentRecordPerActiveCollaborator()
    {
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        FinancialMonthSnapshot snapshot = Snapshot(result: 0, fund: 0);
        MonthlyClose close = MonthlyClose.Create(snapshot, UtcNow);

        IReadOnlyList<MonthlyCloseParticipant> participants = CollaboratorDistributionCalculator.Distribute(
            close, [(first, 6_000), (second, 0)], UtcNow);

        Assert.Equal(2, participants.Count);
        Assert.All(participants, item => Assert.Equal(0, item.Amount.MinorUnits));
        Assert.Contains(participants, item => item.CollaboratorId == first && item.IndividualPercentageBasisPoints == 6_000);
        Assert.Contains(participants, item => item.CollaboratorId == second && item.IndividualPercentageBasisPoints == 0);
        Assert.All(participants, item => Assert.Equal(2_000, item.GlobalPercentageBasisPoints));
    }

    [Fact]
    public void FrozenParticipantPercentages_DoNotDependOnLaterProfileChanges()
    {
        Guid collaboratorId = Guid.NewGuid();
        MonthlyClose close = MonthlyClose.Create(Snapshot(result: 100_000, fund: 20_000), UtcNow);

        MonthlyCloseParticipant participant = Assert.Single(CollaboratorDistributionCalculator.Distribute(
            close, [(collaboratorId, 6_000)], UtcNow));

        Assert.Equal(2_000, participant.GlobalPercentageBasisPoints);
        Assert.Equal(6_000, participant.IndividualPercentageBasisPoints);
        Assert.Equal(12_000, participant.Amount.MinorUnits);
    }

    [Fact]
    public void DistributionPayment_RejectsPartialAndAcceptsExactPendingAmount()
    {
        Money pending = Money.FromDecimal(54m);

        Assert.Throws<InvalidOperationException>(() => DistributionPayment.Create(
            Guid.NewGuid(), new DateOnly(2026, 7, 31), Money.FromDecimal(20m), pending, UtcNow));

        DistributionPayment payment = DistributionPayment.Create(
            Guid.NewGuid(), new DateOnly(2026, 7, 31), pending, pending, UtcNow);
        Assert.Equal(5_400, payment.Amount.MinorUnits);
    }

    [Fact]
    public void FinancialExclusion_RequiresReasonAndKeepsAuditText()
    {
        Assert.Throws<ArgumentException>(() => FinancialCloseExclusion.Create(
            new YearMonth(2026, 7), FinancialCommitmentSource.Obligation, Guid.NewGuid(), " ", UtcNow));

        FinancialCloseExclusion exclusion = FinancialCloseExclusion.Create(
            new YearMonth(2026, 7), FinancialCommitmentSource.Obligation, Guid.NewGuid(), "Factura discutida", UtcNow);
        Assert.Equal("Factura discutida", exclusion.Reason);
    }

    [Fact]
    public void FinancialReserve_CanBeConsumedOnlyOnceAndKeepsExpectedAndActualValues()
    {
        FinancialReserve reserve = FinancialReserve.Create(new YearMonth(2026, 7),
            FinancialCommitmentSource.Maintenance, Guid.NewGuid(), "Aire", new DateOnly(2026, 7, 31),
            Money.FromDecimal(100m), UtcNow);

        reserve.Settle(new DateOnly(2026, 8, 1), Money.FromDecimal(110m), UtcNow.AddDays(1));

        Assert.True(reserve.IsConsumed);
        Assert.Equal(10_000, reserve.ReservedAmount.MinorUnits);
        Assert.Equal(11_000, reserve.ActualAmount?.MinorUnits);
        Assert.Throws<InvalidOperationException>(() => reserve.Settle(
            new DateOnly(2026, 8, 2), Money.FromDecimal(110m), UtcNow.AddDays(2)));
    }

    [Fact]
    public void MonthlyPurchaseItem_CalculatesExpectedTotalAndLinksOnlyOneRealPurchase()
    {
        MonthlyPurchaseItem item = MonthlyPurchaseItem.Create(Guid.NewGuid(), new YearMonth(2026, 7),
            2.5m, Money.FromDecimal(12.40m), true, true, UtcNow);

        Assert.Equal(3_100, item.ExpectedTotalMinorUnits);
        Guid movementId = Guid.NewGuid();
        item.LinkPurchase(movementId, UtcNow.AddMinutes(1));
        Assert.Equal(movementId, item.PurchaseMovementId);
        Assert.Throws<InvalidOperationException>(() => item.LinkPurchase(Guid.NewGuid(), UtcNow.AddMinutes(2)));
    }

    [Fact]
    public void LoanPayment_DecreasesBalanceAndAdvancesAnchoredFrequency()
    {
        Loan loan = Loan.Create("Préstamo", Money.FromDecimal(500m), Money.FromDecimal(50m),
            new DateOnly(2026, 7, 1), LoanFrequency.Monthly, 10, new DateOnly(2026, 7, 31), UtcNow);

        loan.ApplyPayment(Money.FromDecimal(50m), UtcNow.AddDays(1));

        Assert.Equal(45_000, loan.PendingBalance.MinorUnits);
        Assert.Equal(new DateOnly(2026, 8, 31), loan.NextDueDate);
        Assert.Throws<ArgumentOutOfRangeException>(() => loan.ApplyPayment(Money.FromDecimal(451m), UtcNow.AddDays(2)));
    }

    private static FinancialMonthSnapshot Snapshot(long result, long fund) => new(
        new YearMonth(2026, 7), 100_000, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        result, 0, Math.Max(0, -result), fund, Math.Max(0, result) - fund, 2_000, []);
}
