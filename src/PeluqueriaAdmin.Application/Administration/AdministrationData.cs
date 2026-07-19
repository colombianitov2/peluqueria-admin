using PeluqueriaAdmin.Domain.Activity;
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
    IReadOnlyList<DistributionPayment> DistributionPayments,
    IReadOnlyList<Chair> Chairs,
    IReadOnlyList<ActivityRecord> ActivityRecords,
    IReadOnlyList<UnofficialExpense> UnofficialExpenses)
{
    public AdministrationData(
        IReadOnlyList<LocalUsePerson> localUsePeople,
        IReadOnlyList<WeeklyRate> weeklyRates,
        IReadOnlyList<WeeklyCharge> weeklyCharges,
        IReadOnlyList<LocalUsePayment> localUsePayments,
        IReadOnlyList<Product> products,
        IReadOnlyList<InventoryMovement> inventoryMovements,
        IReadOnlyList<MonthlyRestockPlan> restockPlans,
        IReadOnlyList<FinancialEntry> financialEntries,
        IReadOnlyList<Obligation> obligations,
        IReadOnlyList<ObligationPayment> obligationPayments,
        IReadOnlyList<MaintenanceRecord> maintenanceRecords,
        IReadOnlyList<Collaborator> collaborators,
        IReadOnlyList<MonthlyClose> monthlyCloses,
        IReadOnlyList<MonthlyCloseParticipant> monthlyCloseParticipants,
        IReadOnlyList<DistributionPayment> distributionPayments)
        : this(
            localUsePeople, weeklyRates, weeklyCharges, localUsePayments,
            products, inventoryMovements, restockPlans, financialEntries,
            obligations, obligationPayments, maintenanceRecords, collaborators,
            monthlyCloses, monthlyCloseParticipants, distributionPayments,
            [], [], [])
    {
    }
}
