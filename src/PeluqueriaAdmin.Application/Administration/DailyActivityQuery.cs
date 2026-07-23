using PeluqueriaAdmin.Domain.Activity;

namespace PeluqueriaAdmin.Application.Administration;

public static class DailyActivityQuery
{
    public static IReadOnlyList<ActivityRecord> ForLocalDate(
        IEnumerable<ActivityRecord> activities,
        DateOnly localDate,
        TimeZoneInfo timeZone) =>
        activities
            .Where(item => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(item.OccurredUtc, DateTimeKind.Utc),
                    timeZone)) == localDate)
            .OrderByDescending(item => item.OccurredUtc)
            .ToArray();
}
