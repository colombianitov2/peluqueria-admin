namespace PeluqueriaAdmin.Domain.Inventory;

public enum InventoryMovementType
{
    InitialStock = 1,
    Purchase = 2,
    Sale = 3,
    InternalConsumption = 4,
    PhysicalCountAdjustment = 5,
}
