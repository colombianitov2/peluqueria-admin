using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.LocalUse;

public static class WeeklyChargeCalculator
{
    public static IReadOnlyList<DateOnly> ExpectedPeriodStarts(
        DateOnly entryDate,
        DateOnly? exitDate,
        DateOnly throughDate)
    {
        DateOnly lastUseDate = exitDate.HasValue && exitDate.Value < throughDate
            ? exitDate.Value
            : throughDate;
        if (entryDate.AddDays(7) > lastUseDate)
        {
            return [];
        }

        var starts = new List<DateOnly>();
        for (DateOnly start = entryDate; start.AddDays(7) <= lastUseDate; start = start.AddDays(7))
        {
            starts.Add(start);
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

        if (rates.Count == 0)
        {
            throw new InvalidOperationException("Debe existir al menos una tarifa semanal.");
        }

        var existingStarts = existingCharges
            .Where(charge => !charge.IsDeleted && charge.PersonId == person.Id)
            .Select(charge => charge.PeriodStart)
            .ToHashSet();
        WeeklyRate[] orderedRates = rates
            .Where(rate => !rate.IsDeleted)
            .OrderBy(rate => rate.EffectiveFrom)
            .ThenBy(rate => rate.CreatedUtc)
            .ToArray();

        if (orderedRates.Length == 0)
        {
            throw new InvalidOperationException("Debe existir al menos una tarifa semanal vigente.");
        }

        var generated = new List<WeeklyCharge>();

        foreach (DateOnly start in ExpectedPeriodStarts(person.EntryDate, person.ExitDate, throughDate))
        {
            if (existingStarts.Contains(start))
            {
                continue;
            }

            WeeklyRate rate = orderedRates.LastOrDefault(candidate => candidate.EffectiveFrom <= start)
                ?? orderedRates[0];
            generated.Add(WeeklyCharge.Create(person.Id, start, rate.Amount, utcNow));
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
            .Where(item => !item.IsDeleted && item.PeriodEnd <= cutoff)
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
            .Where(item => !item.IsDeleted && item.PersonId == person.Id && item.PeriodEnd <= throughDate)
            .OrderBy(item => item.PeriodEnd)
            .ThenBy(item => item.CreatedUtc)
            .ToArray();
        long totalCharged = completedCharges.Sum(item => item.Amount.MinorUnits);
        long totalPaid = payments
            .Where(item => !item.IsDeleted && item.PersonId == person.Id && item.PaymentDate <= throughDate)
            .Sum(item => item.Amount.MinorUnits);
        long unapplied = totalPaid;
        DateOnly? coveredThrough = null;
        WeeklyRate[] orderedRates = rates
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.EffectiveFrom)
            .ThenBy(item => item.CreatedUtc)
            .ToArray();
        if (orderedRates.Length == 0)
        {
            throw new InvalidOperationException("Debe existir al menos una tarifa semanal vigente.");
        }

        DateOnly nextStart = person.EntryDate;
        while (nextStart.AddDays(7) <= throughDate)
        {
            nextStart = nextStart.AddDays(7);
        }

        bool hasNextCharge = CanCompletePeriod(person, nextStart);
        DateOnly? nextChargeDate = hasNextCharge ? nextStart.AddDays(7) : null;
        long? nextChargeAmount = hasNextCharge ? RateFor(orderedRates, nextStart).Amount.MinorUnits : null;

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
            coveredThrough = charge.PeriodEnd;
        }

        if (!hasNextCharge)
        {
            return BuildBalance(totalCharged, totalPaid, null, null, null, null, coveredThrough);
        }

        long projectedCredit = Math.Max(totalPaid - totalCharged, 0);

        while (CanCompletePeriod(person, nextStart))
        {
            WeeklyRate rate = RateFor(orderedRates, nextStart);
            long weeklyAmount = rate.Amount.MinorUnits;
            if (weeklyAmount > projectedCredit)
            {
                DateOnly periodEnd = nextStart.AddDays(7);
                return BuildBalance(
                    totalCharged,
                    totalPaid,
                    nextChargeDate,
                    nextChargeAmount,
                    WeeklyCharge.PaymentDueDateFor(periodEnd),
                    weeklyAmount - projectedCredit,
                    coveredThrough);
            }

            if (weeklyAmount == 0)
            {
                WeeklyRate? nextPositiveRate = orderedRates.FirstOrDefault(candidate =>
                    candidate.EffectiveFrom > nextStart && candidate.Amount.MinorUnits > 0);
                if (nextPositiveRate is null)
                {
                    return BuildBalance(
                        totalCharged, totalPaid, nextChargeDate, nextChargeAmount, null, null, coveredThrough);
                }

                int weeksToNextRate = Math.Max(1,
                    (nextPositiveRate.EffectiveFrom.DayNumber - nextStart.DayNumber + 6) / 7);
                nextStart = AddWeeksWithinRange(nextStart, weeksToNextRate);
                continue;
            }

            int affordableWeeks = checked((int)Math.Min(projectedCredit / weeklyAmount, int.MaxValue));
            int segmentWeeks = WeeksUntilRateChange(orderedRates, nextStart);
            int remainingWeeks = WeeksUntilExit(person, nextStart);
            int dateLimitWeeks = (DateOnly.MaxValue.DayNumber - nextStart.DayNumber) / 7;
            int coveredWeeks = Math.Min(
                affordableWeeks,
                Math.Min(segmentWeeks, Math.Min(remainingWeeks, dateLimitWeeks)));
            if (coveredWeeks == 0)
            {
                DateOnly periodEnd = nextStart.AddDays(7);
                return BuildBalance(
                    totalCharged,
                    totalPaid,
                    nextChargeDate,
                    nextChargeAmount,
                    WeeklyCharge.PaymentDueDateFor(periodEnd),
                    weeklyAmount - projectedCredit,
                    coveredThrough);
            }

            projectedCredit -= checked(coveredWeeks * weeklyAmount);
            nextStart = AddWeeksWithinRange(nextStart, coveredWeeks);
            coveredThrough = nextStart;
        }

        return BuildBalance(
            totalCharged, totalPaid, nextChargeDate, nextChargeAmount, null, null, coveredThrough);
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
            nextChargeAmount.HasValue ? Money.FromMinorUnits(nextChargeAmount.Value) : null,
            nextRequiredDate,
            nextRequiredAmount.HasValue ? Money.FromMinorUnits(nextRequiredAmount.Value) : null,
            coveredThrough);

    private static WeeklyRate RateFor(WeeklyRate[] rates, DateOnly periodStart) =>
        rates.LastOrDefault(candidate => candidate.EffectiveFrom <= periodStart) ?? rates[0];

    private static bool CanCompletePeriod(LocalUsePerson person, DateOnly periodStart) =>
        periodStart.DayNumber <= DateOnly.MaxValue.DayNumber - 7
        && (!person.ExitDate.HasValue || periodStart.AddDays(7) <= person.ExitDate.Value);

    private static int WeeksUntilRateChange(WeeklyRate[] rates, DateOnly periodStart)
    {
        WeeklyRate? next = rates.FirstOrDefault(candidate => candidate.EffectiveFrom > periodStart);
        return next is null
            ? int.MaxValue
            : Math.Max(1, (next.EffectiveFrom.DayNumber - periodStart.DayNumber + 6) / 7);
    }

    private static int WeeksUntilExit(LocalUsePerson person, DateOnly periodStart)
    {
        if (!person.ExitDate.HasValue)
        {
            return int.MaxValue;
        }

        return Math.Max(0, (person.ExitDate.Value.DayNumber - periodStart.DayNumber) / 7);
    }

    private static DateOnly AddWeeksWithinRange(DateOnly date, int weeks)
    {
        int maximumWeeks = (DateOnly.MaxValue.DayNumber - date.DayNumber) / 7;
        return date.AddDays(checked(7 * Math.Min(weeks, maximumWeeks)));
    }
}
