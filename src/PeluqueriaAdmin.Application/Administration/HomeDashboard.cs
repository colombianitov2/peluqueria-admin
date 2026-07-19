using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Administration;

public sealed record PendingHomeObligation(DateOnly DueDate, string Name);

public sealed record HomeDebt(string Name, Money Amount);

public sealed record HomeDashboard(
    IReadOnlyList<PendingHomeObligation> Obligations,
    IReadOnlyList<HomeDebt> Debts,
    long MissingMinorUnits);

public sealed record ChairCapacity(int Total, int CurrentPeople, int Available, int Overcapacity);

public static class HomeDashboardCalculator
{
    public static HomeDashboard Calculate(
        AdministrationData data,
        Money optionalSuppliesBudget,
        Percentage collaboratorPercentage,
        DateOnly today)
    {
        DateOnly endOfMonth = new YearMonth(today.Year, today.Month).LastDay;
        PendingHomeObligation[] obligations = data.Obligations
            .Where(item => item.Type is ObligationType.Service or ObligationType.Tax
                && item.DueDate <= endOfMonth
                && item.Status(data.ObligationPayments, today) != ObligationStatus.Paid)
            .OrderBy(item => item.DueDate)
            .Select(item => new PendingHomeObligation(item.DueDate, item.Name))
            .ToArray();
        HomeDebt[] debts = data.LocalUsePeople.Select(person => new HomeDebt(
            person.Name,
            WeeklyChargeCalculator.CalculateDebt(
                data.WeeklyCharges.Where(item => item.PersonId == person.Id),
                data.LocalUsePayments.Where(item => item.PersonId == person.Id))))
            .Where(item => item.Amount.MinorUnits > 0)
            .OrderBy(item => item.Name)
            .ToArray();
        MonthlySummaryResult summary = AdministrationReports.MonthlySummary(
            data,
            optionalSuppliesBudget,
            collaboratorPercentage,
            YearMonth.From(today));
        return new HomeDashboard(obligations, debts, summary.MissingMinorUnits);
    }

    public static ChairCapacity Capacity(AdministrationData data, int totalChairs, DateOnly date)
    {
        int current = data.LocalUsePeople.Count(item => item.IsCurrentOn(date));
        return new ChairCapacity(
            totalChairs,
            current,
            Math.Max(0, totalChairs - current),
            Math.Max(0, current - totalChairs));
    }
}
