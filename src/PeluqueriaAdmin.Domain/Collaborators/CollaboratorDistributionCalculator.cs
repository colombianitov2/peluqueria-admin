using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Collaborators;

public static class CollaboratorDistributionCalculator
{
    public static IReadOnlyList<MonthlyCloseParticipant> Distribute(
        MonthlyClose close,
        IEnumerable<(Guid CollaboratorId, int ProfitShareBasisPoints)> allocations,
        DateTime utcNow)
    {
        IReadOnlyDictionary<Guid, long> amounts = CalculateMinorUnitAmounts(
            Math.Max(0, close.BaseResultMinorUnits),
            close.CollaboratorPercentageBasisPoints,
            allocations);
        return amounts
            .OrderBy(item => item.Key)
            .Select(item => new MonthlyCloseParticipant(
                Guid.NewGuid(),
                close.Id,
                item.Key,
                Money.FromMinorUnits(item.Value),
                utcNow))
            .ToArray();
    }

    public static IReadOnlyDictionary<Guid, long> CalculateMinorUnitAmounts(
        long distributableBaseMinorUnits,
        int globalProfitShareBasisPoints,
        IEnumerable<(Guid CollaboratorId, int ProfitShareBasisPoints)> allocations)
    {
        (Guid CollaboratorId, int ProfitShareBasisPoints)[] provided = allocations.ToArray();
        if (provided.Any(item => item.ProfitShareBasisPoints is < 0 or > 10_000))
        {
            throw new ArgumentOutOfRangeException(nameof(allocations), "Cada porcentaje debe estar entre 0 % y 100 %.");
        }
        if (provided.GroupBy(item => item.CollaboratorId).Any(group => group.Count() != 1))
        {
            throw new ArgumentException("Cada colaborador debe aparecer una sola vez.", nameof(allocations));
        }

        (Guid CollaboratorId, int ProfitShareBasisPoints)[] ordered = provided
            .GroupBy(item => item.CollaboratorId)
            .Select(group => group.Single())
            .Where(item => item.ProfitShareBasisPoints > 0)
            .OrderBy(item => item.CollaboratorId)
            .ToArray();

        int totalBasisPoints = ordered.Sum(item => item.ProfitShareBasisPoints);
        if (totalBasisPoints > globalProfitShareBasisPoints)
        {
            throw new InvalidOperationException("La suma de porcentajes individuales supera el porcentaje global configurado.");
        }

        long distributableBase = Math.Max(0, distributableBaseMinorUnits);
        if (ordered.Length == 0 || distributableBase == 0)
        {
            return new Dictionary<Guid, long>();
        }

        long targetMinorUnits = checked((long)decimal.Round(
            distributableBase * totalBasisPoints / 10_000m,
            0,
            MidpointRounding.AwayFromZero));
        var shares = ordered
            .Select(item => new
            {
                item.CollaboratorId,
                BaseAmount = checked(distributableBase * item.ProfitShareBasisPoints / 10_000),
                Remainder = checked(distributableBase * item.ProfitShareBasisPoints % 10_000),
            })
            .ToArray();
        long unassignedMinorUnits = targetMinorUnits - shares.Sum(item => item.BaseAmount);
        HashSet<Guid> roundUp = shares
            .OrderByDescending(item => item.Remainder)
            .ThenBy(item => item.CollaboratorId)
            .Take(checked((int)unassignedMinorUnits))
            .Select(item => item.CollaboratorId)
            .ToHashSet();

        return shares.ToDictionary(
            item => item.CollaboratorId,
            item => item.BaseAmount + (roundUp.Contains(item.CollaboratorId) ? 1 : 0));
    }
}
