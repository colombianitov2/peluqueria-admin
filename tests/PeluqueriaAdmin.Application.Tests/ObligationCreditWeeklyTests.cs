using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Localization;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Tests;

public sealed class ObligationCreditWeeklyTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void SpanishText_ExportsCreditAndWeeklyWithUserFacingNames()
    {
        Assert.Equal("Crédito", SpanishText.For(ObligationType.Credit));
        Assert.Equal("Semanal", SpanishText.For(RecurrenceFrequency.Weekly));
    }

    [Fact]
    public void MonthlyExpenses_SeparatesCreditPaymentsWithoutChangingTheTotal()
    {
        Obligation credit = Obligation.Create(
            "Crédito comercial",
            ObligationType.Credit,
            new DateOnly(2026, 7, 23),
            Money.FromDecimal(50m),
            RecurrenceFrequency.Weekly,
            UtcNow);
        ObligationPayment payment = ObligationPayment.Create(
            credit.Id,
            new DateOnly(2026, 7, 23),
            Money.FromDecimal(40m),
            UtcNow);
        AdministrationData data = EmptyData([credit], [payment]);

        MonthlyExpenseBreakdown result = AdministrationReports.MonthlyExpenses(
            data,
            new YearMonth(2026, 7));

        Assert.Equal(4_000, result.CreditsMinorUnits);
        Assert.Equal(0, result.OtherObligationsMinorUnits);
        Assert.Equal(4_000, result.TotalMinorUnits);
    }

    [Fact]
    public void AnnualReport_DoesNotLeaveExpectedBalanceAfterFinalActualPayment()
    {
        Obligation credit = Obligation.Create(
            "Crédito liquidado",
            ObligationType.Credit,
            new DateOnly(2026, 7, 23),
            Money.FromDecimal(100m),
            RecurrenceFrequency.None,
            UtcNow);
        ObligationPayment payment = ObligationPayment.Create(
            credit.Id,
            new DateOnly(2026, 7, 23),
            Money.FromDecimal(80m),
            UtcNow);
        credit.MarkSettled(UtcNow.AddMinutes(1));

        AnnualAdministrationReport result = AdministrationReports.Annual(
            EmptyData([credit], [payment]),
            Percentage.FromPercent(0m),
            2026);

        Assert.Equal(0, result.Balance.PendingMinorUnits);
        Assert.Equal(8_000, result.Expenses.CreditsMinorUnits);
    }

    private static AdministrationData EmptyData(
        IReadOnlyList<Obligation> obligations,
        IReadOnlyList<ObligationPayment> payments) => new(
            [], [], [], [], [], [], [], [], obligations, payments, [], [], [], [], []);
}
