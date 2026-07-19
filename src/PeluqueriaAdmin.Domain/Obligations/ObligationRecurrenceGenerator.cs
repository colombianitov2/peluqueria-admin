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

        for (int occurrence = 0; ; occurrence++)
        {
            DateOnly dueDate = AtOccurrence(template.DueDate, template.Recurrence, occurrence);
            if (dueDate > throughDate)
            {
                break;
            }

            if (!existingDates.Contains(dueDate))
            {
                generated.Add(Obligation.CreateOccurrence(template, dueDate, utcNow));
            }
        }

        return generated;
    }

    private static DateOnly AtOccurrence(
        DateOnly anchorDate,
        RecurrenceFrequency frequency,
        int occurrence) => frequency switch
        {
            RecurrenceFrequency.Monthly => anchorDate.AddMonths(occurrence),
            RecurrenceFrequency.Annual => anchorDate.AddYears(occurrence),
            _ => throw new InvalidOperationException("La recurrencia no genera nuevas obligaciones."),
        };
}
