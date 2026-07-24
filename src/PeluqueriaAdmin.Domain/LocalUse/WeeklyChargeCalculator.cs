using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.LocalUse;

public static class WeeklyChargeCalculator
{
    public static IReadOnlyList<DateOnly> ExpectedPeriodStarts(
        DateOnly entryDate,
        DateOnly? exitDate,
        DateOnly throughDate)
    {
        var starts = new List<DateOnly>();
        ScheduledPeriod? period = PeriodStarting(entryDate);

        while (period.HasValue
               && period.Value.DueDate <= throughDate
               && CanCharge(period.Value, exitDate))
        {
            starts.Add(period.Value.PeriodStart);
            period = NextPeriod(period.Value);
        }

        return starts;
    }

    public static IReadOnlyList<WeeklyCharge> Generate(
        LocalUsePerson person,
        IEnumerable<WeeklyCharge> existingCharges,
        IReadOnlyCollection<WeeklyRate> rates,
        DateOnly throughDate,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(existingCharges);
        ArgumentNullException.ThrowIfNull(rates);

        WeeklyRate[] orderedRates = ActiveRates(rates);
        HashSet<DateOnly> existingDueDates = existingCharges
            .Where(charge => !charge.IsDeleted && charge.PersonId == person.Id)
            .Select(charge => charge.DueDate)
            .ToHashSet();
        var generated = new List<WeeklyCharge>();

        foreach (DateOnly periodStart in ExpectedPeriodStarts(
                     person.EntryDate,
                     person.ExitDate,
                     throughDate))
        {
            ScheduledPeriod period = PeriodStarting(periodStart)
                ?? throw new InvalidOperationException(
                    "No fue posible calcular el periodo semanal.");
            if (existingDueDates.Contains(period.DueDate))
            {
                continue;
            }

            Money amount = AmountFor(person, period, orderedRates);
            generated.Add(WeeklyCharge.Create(
                person.Id,
                period.PeriodStart,
                amount,
                utcNow));
        }

        return generated;
    }

    public static Money CalculateDebt(
        IEnumerable<WeeklyCharge> charges,
        IEnumerable<LocalUsePayment> payments,
        DateOnly? throughDate = null)
    {
        DateOnly cutoff = throughDate ?? DateOnly.MaxValue;
        long charged = charges
            .Where(item => !item.IsDeleted && item.DueDate <= cutoff)
            .Sum(item => item.Amount.MinorUnits);
        long paid = payments
            .Where(item => !item.IsDeleted && item.PaymentDate <= cutoff)
            .Sum(item => item.Amount.MinorUnits);
        return Money.FromMinorUnits(Math.Max(charged - paid, 0));
    }

    public static WorkerAccountBalance CalculateAccount(
        LocalUsePerson person,
        IEnumerable<WeeklyCharge> charges,
        IEnumerable<LocalUsePayment> payments,
        IReadOnlyCollection<WeeklyRate> rates,
        DateOnly throughDate)
    {
        ArgumentNullException.ThrowIfNull(person);
        ArgumentNullException.ThrowIfNull(charges);
        ArgumentNullException.ThrowIfNull(payments);
        ArgumentNullException.ThrowIfNull(rates);

        WeeklyCharge[] completedCharges = charges
            .Where(item => !item.IsDeleted
                && item.PersonId == person.Id
                && item.DueDate <= throughDate)
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.CreatedUtc)
            .ToArray();
        long totalCharged = completedCharges.Sum(item => item.Amount.MinorUnits);
        long totalPaid = payments
            .Where(item => !item.IsDeleted
                && item.PersonId == person.Id
                && item.PaymentDate <= throughDate)
            .Sum(item => item.Amount.MinorUnits);
        long unapplied = totalPaid;
        DateOnly? coveredThrough = null;
        WeeklyRate[] orderedRates = ActiveRates(rates);
        ScheduledPeriod? next = NextPeriodAfter(person, throughDate);
        DateOnly? nextChargeDate = next?.DueDate;
        long? nextChargeAmount = next.HasValue
            ? AmountFor(person, next.Value, orderedRates).MinorUnits
            : null;

        foreach (WeeklyCharge charge in completedCharges)
        {
            if (unapplied < charge.Amount.MinorUnits)
            {
                return BuildBalance(
                    totalCharged,
                    totalPaid,
                    nextChargeDate,
                    nextChargeAmount,
                    charge.DueDate,
                    charge.Amount.MinorUnits - unapplied,
                    coveredThrough);
            }

            unapplied -= charge.Amount.MinorUnits;
            coveredThrough = charge.DueDate;
        }

        if (!next.HasValue)
        {
            return BuildBalance(
                totalCharged,
                totalPaid,
                null,
                null,
                null,
                null,
                coveredThrough);
        }

        long projectedCredit = Math.Max(totalPaid - totalCharged, 0);
        ScheduledPeriod? projection = next;

        while (projection.HasValue)
        {
            long amount = AmountFor(
                person,
                projection.Value,
                orderedRates).MinorUnits;
            if (amount > projectedCredit)
            {
                return BuildBalance(
                    totalCharged,
                    totalPaid,
                    nextChargeDate,
                    nextChargeAmount,
                    projection.Value.DueDate,
                    amount - projectedCredit,
                    coveredThrough);
            }

            projectedCredit -= amount;
            coveredThrough = projection.Value.DueDate;
            projection = NextChargeablePeriod(
                projection.Value,
                person.ExitDate);
        }

        return BuildBalance(
            totalCharged,
            totalPaid,
            nextChargeDate,
            nextChargeAmount,
            null,
            null,
            coveredThrough);
    }

    private static WorkerAccountBalance BuildBalance(
        long charged,
        long paid,
        DateOnly? nextChargeDate,
        long? nextChargeAmount,
        DateOnly? nextRequiredDate,
        long? nextRequiredAmount,
        DateOnly? coveredThrough) => new(
            Money.FromMinorUnits(Math.Max(charged - paid, 0)),
            Money.FromMinorUnits(Math.Max(paid - charged, 0)),
            Money.FromMinorUnits(charged),
            Money.FromMinorUnits(paid),
            nextChargeDate,
            nextChargeAmount.HasValue
                ? Money.FromMinorUnits(nextChargeAmount.Value)
                : null,
            nextRequiredDate,
            nextRequiredAmount.HasValue
                ? Money.FromMinorUnits(nextRequiredAmount.Value)
                : null,
            coveredThrough);

    private static WeeklyRate[] ActiveRates(
        IReadOnlyCollection<WeeklyRate> rates)
    {
        WeeklyRate[] ordered = rates
            .Where(rate => !rate.IsDeleted)
            .OrderBy(rate => rate.EffectiveFrom)
            .ThenBy(rate => rate.CreatedUtc)
            .ToArray();
        if (ordered.Length == 0)
        {
            throw new InvalidOperationException(
                "Debe existir al menos una tarifa semanal vigente.");
        }

        return ordered;
    }

    private static Money AmountFor(
        LocalUsePerson person,
        ScheduledPeriod period,
        WeeklyRate[] rates)
    {
        WeeklyRate rate = RateFor(rates, period.PeriodStart);
        if (period.PeriodStart != person.EntryDate)
        {
            return rate.Amount;
        }

        int usedDays = period.DueDate.DayNumber
            - period.PeriodStart.DayNumber
            + 1;
        long proratedMinorUnits = checked((long)decimal.Round(
            rate.Amount.MinorUnits * usedDays / 7m,
            0,
            MidpointRounding.AwayFromZero));
        return Money.FromMinorUnits(proratedMinorUnits);
    }

    private static WeeklyRate RateFor(
        WeeklyRate[] rates,
        DateOnly periodStart) =>
        rates.LastOrDefault(candidate =>
            candidate.EffectiveFrom <= periodStart)
        ?? rates[0];

    private static ScheduledPeriod? NextPeriodAfter(
        LocalUsePerson person,
        DateOnly throughDate)
    {
        ScheduledPeriod? period = PeriodStarting(person.EntryDate);
        while (period.HasValue && period.Value.DueDate <= throughDate)
        {
            period = NextPeriod(period.Value);
        }

        return period.HasValue
               && CanCharge(period.Value, person.ExitDate)
            ? period
            : null;
    }

    private static ScheduledPeriod? NextChargeablePeriod(
        ScheduledPeriod current,
        DateOnly? exitDate)
    {
        ScheduledPeriod? next = NextPeriod(current);
        return next.HasValue && CanCharge(next.Value, exitDate)
            ? next
            : null;
    }

    private static ScheduledPeriod? PeriodStarting(DateOnly periodStart)
    {
        int daysUntilSaturday =
            ((int)DayOfWeek.Saturday - (int)periodStart.DayOfWeek + 7) % 7;
        if (periodStart.DayNumber
            > DateOnly.MaxValue.DayNumber - daysUntilSaturday)
        {
            return null;
        }

        DateOnly dueDate = periodStart.AddDays(daysUntilSaturday);
        return new ScheduledPeriod(periodStart, dueDate);
    }

    private static ScheduledPeriod? NextPeriod(ScheduledPeriod current)
    {
        if (current.DueDate == DateOnly.MaxValue)
        {
            return null;
        }

        DateOnly nextStart = current.DueDate.AddDays(1);
        if (nextStart.DayNumber > DateOnly.MaxValue.DayNumber - 6)
        {
            return null;
        }

        return new ScheduledPeriod(nextStart, nextStart.AddDays(6));
    }

    private static bool CanCharge(
        ScheduledPeriod period,
        DateOnly? exitDate) =>
        !exitDate.HasValue || period.DueDate < exitDate.Value;

    private readonly record struct ScheduledPeriod(
        DateOnly PeriodStart,
        DateOnly DueDate);
}
