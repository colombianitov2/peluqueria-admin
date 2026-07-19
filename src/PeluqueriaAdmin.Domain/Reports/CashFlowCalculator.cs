namespace PeluqueriaAdmin.Domain.Reports;

public static class CashFlowCalculator
{
    public static long Balance(IEnumerable<CashMovement> movements, DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new ArgumentException("La fecha final no puede ser anterior a la inicial.", nameof(to));
        }

        return movements
            .Where(movement => movement.Date >= from && movement.Date <= to)
            .Sum(movement => movement.SignedMinorUnits);
    }
}
