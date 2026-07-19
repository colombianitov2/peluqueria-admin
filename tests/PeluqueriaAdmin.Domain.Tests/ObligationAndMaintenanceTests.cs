using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Tests;

public sealed class ObligationAndMaintenanceTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(RecurrenceFrequency.Monthly, 4)]
    [InlineData(RecurrenceFrequency.Annual, 2)]
    public void Recurrence_IsIdempotent(RecurrenceFrequency recurrence, int expectedCount)
    {
        DateOnly dueDate = new(2026, 1, 15);
        Obligation template = Obligation.Create(
            "Electricidad", ObligationType.Service, dueDate, Money.FromDecimal(100m), recurrence, UtcNow);
        DateOnly through = recurrence == RecurrenceFrequency.Monthly
            ? dueDate.AddMonths(3)
            : dueDate.AddYears(1);

        IReadOnlyList<Obligation> first = ObligationRecurrenceGenerator.Generate(
            template, [template], through, UtcNow);
        IReadOnlyList<Obligation> second = ObligationRecurrenceGenerator.Generate(
            template, new[] { template }.Concat(first), through, UtcNow);

        Assert.Equal(expectedCount - 1, first.Count);
        Assert.Empty(second);
        Assert.All(first, item => Assert.Equal(template.SeriesId, item.SeriesId));
    }

    [Fact]
    public void Obligation_UsesExpectedUntilFullyPaidThenUsesActualWithoutDuplication()
    {
        Obligation obligation = Obligation.Create(
            "Impuesto", ObligationType.Tax, new DateOnly(2026, 7, 20),
            Money.FromDecimal(100m), RecurrenceFrequency.None, UtcNow);
        ObligationPayment partial = ObligationPayment.Create(
            obligation.Id, new DateOnly(2026, 7, 10), Money.FromDecimal(40m), UtcNow);
        ObligationPayment final = ObligationPayment.Create(
            obligation.Id, new DateOnly(2026, 7, 15), Money.FromDecimal(65m), UtcNow);

        Assert.Equal(10_000, obligation.GoalAmount([partial]).MinorUnits);
        Assert.Equal(ObligationStatus.Partial, obligation.Status([partial], new DateOnly(2026, 7, 18)));
        Assert.Equal(10_500, obligation.GoalAmount([partial, final]).MinorUnits);
        Assert.Equal(ObligationStatus.Paid, obligation.Status([partial, final], new DateOnly(2026, 7, 18)));
    }

    [Fact]
    public void MonthlyRecurrence_AnchoredOnDay31DoesNotDriftAfterFebruary()
    {
        Obligation template = Obligation.Create(
            "Servicio", ObligationType.Service, new DateOnly(2026, 1, 31),
            Money.FromDecimal(10m), RecurrenceFrequency.Monthly, UtcNow);

        IReadOnlyList<Obligation> generated = ObligationRecurrenceGenerator.Generate(
            template, [template], new DateOnly(2026, 5, 31), UtcNow);

        Assert.Equal(
            [new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31), new DateOnly(2026, 4, 30), new DateOnly(2026, 5, 31)],
            generated.Select(item => item.DueDate));
    }

    [Fact]
    public void Maintenance_UsesEstimatedOrActualButNeverBoth()
    {
        var july = new YearMonth(2026, 7);
        MaintenanceRecord pending = MaintenanceRecord.Create(
            "Aire", "Limpieza", new DateOnly(2026, 7, 20), Money.FromDecimal(80m), null, null, UtcNow);
        MaintenanceRecord completed = MaintenanceRecord.Create(
            "Aire", "Reparación", new DateOnly(2026, 7, 10), Money.FromDecimal(100m),
            new DateOnly(2026, 7, 11), Money.FromDecimal(120m), UtcNow);

        Assert.Equal(8_000, pending.GoalAmountFor(july).MinorUnits);
        Assert.Equal(12_000, completed.GoalAmountFor(july).MinorUnits);
        Assert.True(pending.NeedsAttention(new DateOnly(2026, 7, 21)));
        Assert.False(completed.NeedsAttention(new DateOnly(2026, 7, 21)));
    }
}
