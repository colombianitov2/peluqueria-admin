using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Domain.Inventory;

public static class InventoryCalculator
{
    public static decimal CurrentQuantity(IEnumerable<InventoryMovement> movements) =>
        movements.Where(movement => !movement.IsDeleted).Sum(movement => movement.QuantityDelta);

    public static Money AverageUnitCost(IEnumerable<InventoryMovement> movements)
    {
        InventoryMovement[] entries = movements
            .Where(movement => !movement.IsDeleted
                && movement.QuantityDelta > 0m
                && movement.EstimatedCost.HasValue
                && movement.Type is InventoryMovementType.InitialStock or InventoryMovementType.Purchase)
            .ToArray();
        decimal quantity = entries.Sum(item => item.QuantityDelta);
        if (quantity == 0m)
        {
            return Money.FromMinorUnits(0);
        }

        long cost = entries.Sum(item => item.EstimatedCost!.Value.MinorUnits);
        long averageMinorUnits = checked((long)decimal.Round(cost / quantity, 0, MidpointRounding.AwayFromZero));
        return Money.FromMinorUnits(averageMinorUnits);
    }

    public static void EnsureNonNegative(IEnumerable<InventoryMovement> movements)
    {
        decimal running = 0m;
        foreach (InventoryMovement movement in movements
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Date)
            .ThenBy(item => item.CreatedUtc))
        {
            running += movement.QuantityDelta;
            if (running < 0m)
            {
                throw new InvalidOperationException("Los movimientos dejarían el inventario en negativo.");
            }
        }
    }
}
