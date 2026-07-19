using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Collaborators;

public static class CollaboratorDistributionCalculator
{
    public static IReadOnlyList<MonthlyCloseParticipant> Distribute(
        MonthlyClose close,
        IEnumerable<Guid> collaboratorIds,
        DateTime utcNow)
    {
        Guid[] orderedIds = collaboratorIds.Distinct().OrderBy(id => id).ToArray();
        if (orderedIds.Length == 0 || close.FundMinorUnits == 0)
        {
            return [];
        }

        long baseAmount = close.FundMinorUnits / orderedIds.Length;
        long remainder = close.FundMinorUnits % orderedIds.Length;

        return orderedIds
            .Select((collaboratorId, index) => new MonthlyCloseParticipant(
                Guid.NewGuid(),
                close.Id,
                collaboratorId,
                Money.FromMinorUnits(baseAmount + (index < remainder ? 1 : 0)),
                utcNow))
            .ToArray();
    }
}
