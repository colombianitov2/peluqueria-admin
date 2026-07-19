using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.LocalUse;

public static class WeeklyChargeCalculator
{
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

        DateOnly lastPermittedStart = person.ExitDate.HasValue && person.ExitDate.Value < throughDate
            ? person.ExitDate.Value
            : throughDate;
        var generated = new List<WeeklyCharge>();

        for (DateOnly start = person.EntryDate; start <= lastPermittedStart; start = start.AddDays(7))
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
        IEnumerable<LocalUsePayment> payments)
    {
        long charged = charges.Where(item => !item.IsDeleted).Sum(item => item.Amount.MinorUnits);
        long paid = payments.Where(item => !item.IsDeleted).Sum(item => item.Amount.MinorUnits);
        if (paid > charged)
        {
            throw new InvalidOperationException("Los pagos registrados superan las cuotas generadas.");
        }

        return Money.FromMinorUnits(charged - paid);
    }
}
