using PeluqueriaAdmin.Domain.Common;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed record OperationRow(
    string Date,
    string Principal,
    string Detail,
    string Quantity,
    string Amount,
    string Status,
    AuditableEntity? Entity);
