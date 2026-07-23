using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Administration;

public sealed record PendingHomeObligation(
    DateOnly DueDate,
    string Name,
    string Type,
    Money Amount,
    string Description,
    string Status)
{
    public PendingHomeObligation(DateOnly dueDate, string name)
        : this(dueDate, name, "Obligación", Money.FromMinorUnits(0), string.Empty, "Pendiente")
    {
    }
}

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
        Percentage collaboratorPercentage,
        DateOnly today)
    {
        DateOnly endOfMonth = new YearMonth(today.Year, today.Month).LastDay;
        PendingHomeObligation[] obligations = data.Obligations
            .Where(item => item.DueDate <= endOfMonth
                && item.Status(data.ObligationPayments, today) != ObligationStatus.Paid)
            .OrderBy(item => item.DueDate)
            .Select(item =>
            {
                long paid = data.ObligationPayments
                    .Where(payment => payment.ObligationId == item.Id)
                    .Sum(payment => payment.Amount.MinorUnits);
                return new PendingHomeObligation(
                    item.DueDate,
                    item.Name,
                    item.Type switch
                    {
                        ObligationType.Service => "Servicio",
                        ObligationType.Tax => "Impuesto",
                        _ => "Otra obligación",
                    },
                    Money.FromMinorUnits(Math.Max(0, item.ExpectedAmount.MinorUnits - paid)),
                    item.Description ?? string.Empty,
                    item.DueDate < today ? "Vencido" : "Pendiente");
            })
            .Concat(data.LoanInstallments
                .Where(item => item.DueDate <= endOfMonth
                    && !data.LoanPayments.Any(payment => payment.InstallmentId == item.Id))
                .Select(item =>
                {
                    Loan? loan = data.Loans.SingleOrDefault(candidate => candidate.Id == item.LoanId);
                    return new PendingHomeObligation(
                        item.DueDate,
                        loan?.Name ?? "Préstamo",
                        "Cuota de préstamo",
                        item.Amount,
                        item.Description ?? string.Empty,
                        item.DueDate < today ? "Vencido" : "Pendiente");
                }))
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.Name)
            .ToArray();
        HomeDebt[] debts = data.LocalUsePeople.Select(person => new HomeDebt(
            person.Name,
            WeeklyChargeCalculator.CalculateDebt(
                data.WeeklyCharges.Where(item => item.PersonId == person.Id),
                data.LocalUsePayments.Where(item => item.PersonId == person.Id),
                today)))
            .Where(item => item.Amount.MinorUnits > 0)
            .OrderBy(item => item.Name)
            .ToArray();
        MonthlySummaryResult summary = AdministrationReports.MonthlySummary(
            data,
            collaboratorPercentage,
            YearMonth.From(today));
        return new HomeDashboard(obligations, debts, summary.MissingMinorUnits);
    }

    public static ChairCapacity Capacity(AdministrationData data, DateOnly date)
    {
        int current = data.LocalUsePeople.Count(item => item.IsCurrentOn(date));
        int totalChairs = data.Chairs.Count;
        int occupied = data.Chairs.Count(item => item.AssignedPersonId.HasValue
            && data.LocalUsePeople.Any(person => person.Id == item.AssignedPersonId && person.IsCurrentOn(date)));
        return new ChairCapacity(
            totalChairs,
            current,
            Math.Max(0, totalChairs - occupied),
            Math.Max(0, current - totalChairs));
    }

    public static ChairCapacity Capacity(AdministrationData data, int legacyTotalChairs, DateOnly date)
    {
        _ = legacyTotalChairs;
        return Capacity(data, date);
    }
}
