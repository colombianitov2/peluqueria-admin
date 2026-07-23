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
        DateOnly dueDate = NextSaturdayAfter(entryDate);
        while (dueDate <= throughDate && (!exitDate.HasValue || dueDate < exitDate.Value))
        {
            starts.Add(dueDate.AddDays(-6));
            dueDate = dueDate.AddDays(7);
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

        var existingDueDates = existingCharges
            .Where(charge => !charge.IsDeleted && charge.PersonId == person.Id)
            .Select(charge => charge.DueDate)
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
            DateOnly dueDate = start.AddDays(6);
            if (existingDueDates.Contains(dueDate))
            {
                continue;
            }

            WeeklyRate rate = orderedRates.LastOrDefault(candidate => candidate.EffectiveFrom <= dueDate)
                ?? orderedRates[0];
            generated.Add(WeeklyCharge.CreateForDueDate(person.Id, dueDate, rate.Amount, utcNow));
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
            .Where(item => !item.IsDeleted && item.PersonId == person.Id && item.DueDate <= throughDate)
            .OrderBy(item => item.DueDate)
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

        DateOnly nextDueDate = NextSaturdayAfter(throughDate);
        bool hasNextCharge = CanChargeOn(person, nextDueDate);
        DateOnly? nextChargeDate = hasNextCharge
            ? nextDueDate
            : null;
        long? nextChargeAmount = hasNextCharge ? RateFor(orderedRates, nextDueDate).Amount.MinorUnits : null;

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

        if (!hasNextCharge)
        {
            return BuildBalance(totalCharged, totalPaid, null, null, null, null, coveredThrough);
        }

        long projectedCredit = Math.Max(totalPaid - totalCharged, 0);

        DateOnly projectionDueDate = nextDueDate;
        while (CanChargeOn(person, projectionDueDate))
        {
            WeeklyRate rate = RateFor(orderedRates, projectionDueDate);
            long weeklyAmount = rate.Amount.MinorUnits;
            if (weeklyAmount > projectedCredit)
            {
                return BuildBalance(
                    totalCharged,
                    totalPaid,
                    nextChargeDate,
                    nextChargeAmount,
                    projectionDueDate,
                    weeklyAmount - projectedCredit,
                    coveredThrough);
            }

            if (weeklyAmount == 0)
            {
                WeeklyRate? nextPositiveRate = orderedRates.FirstOrDefault(candidate =>
                    candidate.EffectiveFrom > projectionDueDate && candidate.Amount.MinorUnits > 0);
                if (nextPositiveRate is null)
                {
                    return BuildBalance(
                        totalCharged, totalPaid, nextChargeDate, nextChargeAmount, null, null, coveredThrough);
                }

                int weeksToNextRate = Math.Max(1,
                    (nextPositiveRate.EffectiveFrom.DayNumber - projectionDueDate.DayNumber + 6) / 7);
                projectionDueDate = AddWeeksWithinRange(projectionDueDate, weeksToNextRate);
                continue;
            }

            int affordableWeeks = checked((int)Math.Min(projectedCredit / weeklyAmount, int.MaxValue));
            int segmentWeeks = WeeksUntilRateChange(orderedRates, projectionDueDate);
            int remainingWeeks = WeeksUntilExit(person, projectionDueDate);
            int dateLimitWeeks = (DateOnly.MaxValue.DayNumber - projectionDueDate.DayNumber) / 7;
            int coveredWeeks = Math.Min(
                affordableWeeks,
                Math.Min(segmentWeeks, Math.Min(remainingWeeks, dateLimitWeeks)));
            if (coveredWeeks == 0)
            {
                return BuildBalance(
                    totalCharged,
                    totalPaid,
                    nextChargeDate,
                    nextChargeAmount,
                    projectionDueDate,
                    weeklyAmount - projectedCredit,
                    coveredThrough);
            }

            projectedCredit -= checked(coveredWeeks * weeklyAmount);
            coveredThrough = AddWeeksWithinRange(projectionDueDate, coveredWeeks - 1);
            projectionDueDate = AddWeeksWithinRange(projectionDueDate, coveredWeeks);
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

    private static WeeklyRate RateFor(WeeklyRate[] rates, DateOnly dueDate) =>
        rates.LastOrDefault(candidate => candidate.EffectiveFrom <= dueDate) ?? rates[0];

    private static bool CanChargeOn(LocalUsePerson person, DateOnly dueDate) =>
        dueDate <= DateOnly.MaxValue.AddDays(-7)
        && (!person.ExitDate.HasValue || dueDate < person.ExitDate.Value);

    private static int WeeksUntilRateChange(WeeklyRate[] rates, DateOnly dueDate)
    {
        WeeklyRate? next = rates.FirstOrDefault(candidate => candidate.EffectiveFrom > dueDate);
        return next is null
            ? int.MaxValue
            : Math.Max(1, (next.EffectiveFrom.DayNumber - dueDate.DayNumber + 6) / 7);
    }

    private static int WeeksUntilExit(LocalUsePerson person, DateOnly dueDate)
    {
        if (!person.ExitDate.HasValue)
        {
            return int.MaxValue;
        }

        return Math.Max(0, (person.ExitDate.Value.DayNumber - dueDate.DayNumber + 6) / 7);
    }

    private static DateOnly AddWeeksWithinRange(DateOnly date, int weeks)
    {
        int maximumWeeks = (DateOnly.MaxValue.DayNumber - date.DayNumber) / 7;
        return date.AddDays(checked(7 * Math.Min(weeks, maximumWeeks)));
    }

    private static DateOnly NextSaturdayAfter(DateOnly date)
    {
        int days = ((int)DayOfWeek.Saturday - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(days == 0 ? 7 : days);
    }
}
