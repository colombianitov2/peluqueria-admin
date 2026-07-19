using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;

namespace PeluqueriaAdmin.Application.Administration;

public sealed record AdministrationData(
    IReadOnlyList<LocalUsePerson> LocalUsePeople,
    IReadOnlyList<WeeklyRate> WeeklyRates,
    IReadOnlyList<WeeklyCharge> WeeklyCharges,
    IReadOnlyList<LocalUsePayment> LocalUsePayments,
    IReadOnlyList<Product> Products,
    IReadOnlyList<InventoryMovement> InventoryMovements,
    IReadOnlyList<MonthlyRestockPlan> RestockPlans,
    IReadOnlyList<FinancialEntry> FinancialEntries,
    IReadOnlyList<Obligation> Obligations,
    IReadOnlyList<ObligationPayment> ObligationPayments,
    IReadOnlyList<MaintenanceRecord> MaintenanceRecords,
    IReadOnlyList<Collaborator> Collaborators,
    IReadOnlyList<MonthlyClose> MonthlyCloses,
    IReadOnlyList<MonthlyCloseParticipant> MonthlyCloseParticipants,
    IReadOnlyList<DistributionPayment> DistributionPayments);
