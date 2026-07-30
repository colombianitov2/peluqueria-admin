using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.LocalUse;

public static class DailyChargeCalculator
{
    public static IReadOnlyList<DailyCharge> Generate(
        LocalUsePerson person,
        IEnumerable<DailyCharge> existingCharges,
        IReadOnlyCollection<DailyRate> rates,
        IReadOnlyCollection<ChairAssignmentPeriod> assignments,
        DateOnly throughDate,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(person);
        DailyRate[] orderedRates = ActiveRates(rates);
        HashSet<DateOnly> existingDates = existingCharges
            .Where(item => !item.IsDeleted && item.PersonId == person.Id)
            .Select(item => item.ChargeDate)
            .ToHashSet();
        var result = new List<DailyCharge>();

        foreach (ChairAssignmentPeriod assignment in assignments
            .Where(item => !item.IsDeleted && item.PersonId == person.Id)
            .OrderBy(item => item.StartDate))
        {
            DateOnly last = Min(
                throughDate,
                person.ExitDate?.AddDays(-1) ?? DateOnly.MaxValue,
                assignment.EndDateExclusive?.AddDays(-1) ?? DateOnly.MaxValue);
            DateOnly first = assignment.StartDate > person.EntryDate
                ? assignment.StartDate
                : person.EntryDate;
            for (DateOnly date = first; date <= last; date = date.AddDays(1))
            {
                if (!person.IsCurrentOn(date)
                    || !IsChargeableDay(date)
                    || existingDates.Contains(date))
                {
                    continue;
                }

                DailyRate? rate = RateFor(orderedRates, date);
                if (rate?.Amount is not Money amount)
                {
                    continue;
                }

                result.Add(DailyCharge.Create(
                    person.Id,
                    assignment.ChairId,
                    rate.Id,
                    date,
                    amount,
                    utcNow));
                existingDates.Add(date);
            }
        }

        return result;
    }

    public static WorkerAccountBalance CalculateAccount(
        LocalUsePerson person,
        IEnumerable<DailyCharge> dailyCharges,
        IEnumerable<WeeklyCharge> legacyWeeklyCharges,
        IEnumerable<LocalUsePayment> payments,
        IReadOnlyCollection<DailyRate> rates,
        IReadOnlyCollection<ChairAssignmentPeriod> assignments,
        DateOnly throughDate)
    {
        DailyCharge[] currentDaily = dailyCharges
            .Where(item => !item.IsDeleted && item.PersonId == person.Id && item.ChargeDate <= throughDate)
            .OrderBy(item => item.ChargeDate)
            .ThenBy(item => item.CreatedUtc)
            .ToArray();
        WeeklyCharge[] legacy = legacyWeeklyCharges
            .Where(item => !item.IsDeleted && item.PersonId == person.Id && item.DueDate <= throughDate)
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.CreatedUtc)
            .ToArray();
        LocalUsePayment[] currentPayments = payments
            .Where(item => !item.IsDeleted && item.PersonId == person.Id && item.PaymentDate <= throughDate)
            .OrderBy(item => item.PaymentDate)
            .ThenBy(item => item.CreatedUtc)
            .ToArray();
        long charged = checked(
            currentDaily.Sum(item => item.Amount.MinorUnits)
            + legacy.Sum(item => item.Amount.MinorUnits));
        long paid = currentPayments.Sum(item => item.Amount.MinorUnits);
        long debt = Math.Max(charged - paid, 0);
        long credit = Math.Max(paid - charged, 0);
        DailyRate[] orderedRates = ActiveRates(rates);
        Money? currentRate = RateFor(orderedRates, throughDate)?.Amount;

        (DateOnly? nextChargeDate, Money? nextChargeAmount) = NextCharge(
            person,
            currentDaily,
            orderedRates,
            assignments,
            throughDate);
        if (debt > 0)
        {
            DateOnly required = OldestOutstandingDueDate(currentDaily, legacy, paid)
                ?? nextChargeDate
                ?? throughDate;
            return new WorkerAccountBalance(
                Money.FromMinorUnits(debt),
                Money.FromMinorUnits(0),
                Money.FromMinorUnits(charged),
                Money.FromMinorUnits(paid),
                nextChargeDate,
                nextChargeAmount,
                required,
                Money.FromMinorUnits(debt),
                LastFullyCoveredDate(currentDaily, legacy, paid),
                currentRate);
        }

        Projection projection = ProjectCredit(
            person,
            assignments,
            orderedRates,
            throughDate,
            credit);
        return new WorkerAccountBalance(
            Money.FromMinorUnits(0),
            Money.FromMinorUnits(credit),
            Money.FromMinorUnits(charged),
            Money.FromMinorUnits(paid),
            nextChargeDate,
            nextChargeAmount,
            projection.RequiredPaymentDate,
            projection.RequiredPaymentAmount.HasValue
                ? Money.FromMinorUnits(projection.RequiredPaymentAmount.Value)
                : null,
            projection.CoveredThroughDate ?? LastFullyCoveredDate(currentDaily, legacy, paid),
            currentRate);
    }

    public static Money CalculateDebt(
        IEnumerable<DailyCharge> dailyCharges,
        IEnumerable<WeeklyCharge> legacyWeeklyCharges,
        IEnumerable<LocalUsePayment> payments,
        DateOnly throughDate)
    {
        long charged = checked(
            dailyCharges.Where(item => !item.IsDeleted && item.ChargeDate <= throughDate)
                .Sum(item => item.Amount.MinorUnits)
            + legacyWeeklyCharges.Where(item => !item.IsDeleted && item.DueDate <= throughDate)
                .Sum(item => item.Amount.MinorUnits));
        long paid = payments.Where(item => !item.IsDeleted && item.PaymentDate <= throughDate)
            .Sum(item => item.Amount.MinorUnits);
        return Money.FromMinorUnits(Math.Max(charged - paid, 0));
    }

    public static bool IsChargeableDay(DateOnly date) => date.DayOfWeek != DayOfWeek.Sunday;

    public static DailyRate? RateFor(IReadOnlyCollection<DailyRate> rates, DateOnly date) =>
        ActiveRates(rates).LastOrDefault(item => item.EffectiveDate <= date);

    private static Projection ProjectCredit(
        LocalUsePerson person,
        IReadOnlyCollection<ChairAssignmentPeriod> assignments,
        DailyRate[] rates,
        DateOnly throughDate,
        long credit)
    {
        DateOnly date = throughDate == DateOnly.MaxValue ? throughDate : throughDate.AddDays(1);
        DateOnly horizon = person.ExitDate?.AddDays(-1)
            ?? (date.DayNumber <= DateOnly.MaxValue.DayNumber - 740 ? date.AddDays(740) : DateOnly.MaxValue);
        DateOnly? covered = null;
        while (date <= horizon)
        {
            DateOnly saturday = DailyCharge.DueSaturday(date);
            long weeklyAmount = 0;
            DateOnly? lastChargeable = null;
            for (DateOnly candidate = date; candidate <= saturday && candidate <= horizon; candidate = candidate.AddDays(1))
            {
                if (IsEligible(person, assignments, candidate))
                {
                    Money? amount = RateFor(rates, candidate)?.Amount;
                    if (amount.HasValue)
                    {
                        weeklyAmount = checked(weeklyAmount + amount.Value.MinorUnits);
                        lastChargeable = candidate;
                    }
                }
            }

            if (weeklyAmount > credit)
            {
                return new Projection(
                    saturday,
                    weeklyAmount - credit,
                    covered);
            }

            credit -= weeklyAmount;
            if (lastChargeable.HasValue)
            {
                covered = lastChargeable;
            }

            if (saturday == DateOnly.MaxValue)
            {
                break;
            }
            date = saturday.AddDays(1);
        }

        return new Projection(null, null, covered);
    }

    private static (DateOnly? Date, Money? Amount) NextCharge(
        LocalUsePerson person,
        IReadOnlyCollection<DailyCharge> currentDaily,
        DailyRate[] rates,
        IReadOnlyCollection<ChairAssignmentPeriod> assignments,
        DateOnly throughDate)
    {
        DateOnly? existingDueDate = currentDaily
            .Where(item => item.DueDate >= throughDate)
            .Select(item => (DateOnly?)item.DueDate)
            .OrderBy(item => item)
            .FirstOrDefault();
        DateOnly? projectedDueDate = NextChargeSaturday(person, assignments, rates, throughDate);
        DateOnly? dueDate = existingDueDate.HasValue
            && (!projectedDueDate.HasValue || existingDueDate.Value <= projectedDueDate.Value)
                ? existingDueDate
                : projectedDueDate;
        if (!dueDate.HasValue)
        {
            return (null, null);
        }

        DateOnly weekStart = dueDate.Value.AddDays(-5);
        DailyCharge[] existing = currentDaily
            .Where(item => item.DueDate == dueDate.Value)
            .ToArray();
        HashSet<DateOnly> existingDates = existing.Select(item => item.ChargeDate).ToHashSet();
        long amountMinorUnits = existing.Sum(item => item.Amount.MinorUnits);
        DateOnly firstProjectionDate = throughDate == DateOnly.MaxValue
            ? throughDate
            : throughDate.AddDays(1);
        if (firstProjectionDate < weekStart)
        {
            firstProjectionDate = weekStart;
        }

        for (DateOnly date = firstProjectionDate;
             date <= dueDate.Value;
             date = date.AddDays(1))
        {
            if (existingDates.Contains(date) || !IsEligible(person, assignments, date))
            {
                continue;
            }

            Money? rate = RateFor(rates, date)?.Amount;
            if (rate.HasValue)
            {
                amountMinorUnits = checked(amountMinorUnits + rate.Value.MinorUnits);
            }
        }

        return (dueDate, Money.FromMinorUnits(amountMinorUnits));
    }

    private static DateOnly? NextChargeSaturday(
        LocalUsePerson person,
        IReadOnlyCollection<ChairAssignmentPeriod> assignments,
        IReadOnlyCollection<DailyRate> rates,
        DateOnly throughDate)
    {
        DateOnly horizon = person.ExitDate?.AddDays(-1)
            ?? (throughDate.DayNumber <= DateOnly.MaxValue.DayNumber - 370
                ? throughDate.AddDays(370)
                : DateOnly.MaxValue);
        for (DateOnly date = throughDate; date <= horizon; date = date.AddDays(1))
        {
            if (IsEligible(person, assignments, date)
                && RateFor(rates, date)?.Amount.HasValue == true)
            {
                return DailyCharge.DueSaturday(date);
            }
        }
        return null;
    }

    private static bool IsEligible(
        LocalUsePerson person,
        IReadOnlyCollection<ChairAssignmentPeriod> assignments,
        DateOnly date) =>
        IsChargeableDay(date)
        && person.IsCurrentOn(date)
        && assignments.Any(item => item.PersonId == person.Id && item.AppliesOn(date));

    private static DateOnly? OldestOutstandingDueDate(
        IEnumerable<DailyCharge> daily,
        IEnumerable<WeeklyCharge> legacy,
        long paid)
    {
        var charges = daily
            .Select(item => (item.DueDate, item.CreatedUtc, item.Amount.MinorUnits))
            .Concat(legacy.Select(item => (item.DueDate, item.CreatedUtc, item.Amount.MinorUnits)))
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.CreatedUtc);
        foreach (var charge in charges)
        {
            if (paid < charge.MinorUnits)
            {
                return charge.DueDate;
            }
            paid -= charge.MinorUnits;
        }
        return null;
    }

    private static DateOnly? LastFullyCoveredDate(
        IEnumerable<DailyCharge> daily,
        IEnumerable<WeeklyCharge> legacy,
        long paid)
    {
        DateOnly? covered = null;
        var charges = daily
            .Select(item => (Date: item.ChargeDate, item.CreatedUtc, item.Amount.MinorUnits))
            .Concat(legacy.Select(item => (Date: item.PeriodEnd, item.CreatedUtc, item.Amount.MinorUnits)))
            .OrderBy(item => item.Date)
            .ThenBy(item => item.CreatedUtc);
        foreach (var charge in charges)
        {
            if (paid < charge.MinorUnits)
            {
                break;
            }
            paid -= charge.MinorUnits;
            covered = charge.Date;
        }
        return covered;
    }

    private static DailyRate[] ActiveRates(IReadOnlyCollection<DailyRate> rates)
    {
        DailyRate[] ordered = rates
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.EffectiveDate)
            .ThenBy(item => item.EffectiveFromUtc)
            .ToArray();
        return ordered;
    }

    private static DateOnly Min(DateOnly first, DateOnly second, DateOnly third) =>
        first <= second && first <= third ? first : second <= third ? second : third;

    private sealed record Projection(
        DateOnly? RequiredPaymentDate,
        long? RequiredPaymentAmount,
        DateOnly? CoveredThroughDate);
}
