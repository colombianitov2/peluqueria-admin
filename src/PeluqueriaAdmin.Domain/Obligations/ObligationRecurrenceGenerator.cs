namespace PeluqueriaAdmin.Domain.Obligations;

public static class ObligationRecurrenceGenerator
{
    public static IReadOnlyList<Obligation> Generate(
        Obligation template,
        IEnumerable<Obligation> existing,
        DateOnly throughDate,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.Recurrence == RecurrenceFrequency.None)
        {
            return [];
        }

        var existingDates = existing
            .Where(item => !item.IsDeleted && item.SeriesId == template.SeriesId)
            .Select(item => item.DueDate)
            .ToHashSet();
        var generated = new List<Obligation>();

        for (DateOnly dueDate = template.DueDate; dueDate <= throughDate; dueDate = Next(dueDate, template.Recurrence))
        {
            if (!existingDates.Contains(dueDate))
            {
                generated.Add(Obligation.CreateOccurrence(template, dueDate, utcNow));
            }
        }

        return generated;
    }

    private static DateOnly Next(DateOnly date, RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Monthly => date.AddMonths(1),
        RecurrenceFrequency.Annual => date.AddYears(1),
        _ => throw new InvalidOperationException("La recurrencia no genera nuevas obligaciones."),
    };
}
