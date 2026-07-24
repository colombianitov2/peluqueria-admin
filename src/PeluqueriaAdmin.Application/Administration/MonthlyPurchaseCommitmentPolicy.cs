using PeluqueriaAdmin.Domain.Inventory;

namespace PeluqueriaAdmin.Application.Administration;

public static class MonthlyPurchaseCommitmentPolicy
{
    public static bool IsPending(
        MonthlyPurchaseItem item,
        AdministrationData data,
        DateOnly throughDate)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(data);

        // IsActive y ReserveWhenOutOfStock se conservan únicamente para leer
        // bases anteriores. Desde esta versión, toda fila visible y no comprada
        // de la lista mensual es un compromiso conocido.
        return !item.PurchaseMovementId.HasValue
            && item.Month.LastDay <= throughDate;
    }
}
