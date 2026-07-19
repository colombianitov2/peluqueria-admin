using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed record OperationRow(
    string Date,
    string Principal,
    string Detail,
    string Quantity,
    string Amount,
    string Status,
    AuditableEntity? Entity)
{
    public bool IsInventoryProduct => Entity is Product;

    public bool AllowsInternalConsumption => Entity is Product product && !product.IsForSale;

    public bool IsObligation => Entity is Obligation;

    public bool IsPendingMaintenance => Entity is MaintenanceRecord maintenance && !maintenance.CompletedDate.HasValue;

    public bool CanEdit => Entity is not null and not MonthlyClose and not MonthlyCloseParticipant;
}
