using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Domain.Activity;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Drafts;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Notes;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Infrastructure.Persistence;

public sealed class PeluqueriaDbContext(DbContextOptions<PeluqueriaDbContext> options)
    : DbContext(options)
{
    public DbSet<GeneralSettings> Settings => Set<GeneralSettings>();

    public DbSet<LocalUsePerson> LocalUsePeople => Set<LocalUsePerson>();

    public DbSet<WeeklyRate> WeeklyRates => Set<WeeklyRate>();

    public DbSet<WeeklyCharge> WeeklyCharges => Set<WeeklyCharge>();

    public DbSet<LocalUsePayment> LocalUsePayments => Set<LocalUsePayment>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    public DbSet<MonthlyRestockPlan> RestockPlans => Set<MonthlyRestockPlan>();

    public DbSet<FinancialEntry> FinancialEntries => Set<FinancialEntry>();

    public DbSet<Obligation> Obligations => Set<Obligation>();

    public DbSet<ObligationPayment> ObligationPayments => Set<ObligationPayment>();

    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();

    public DbSet<Collaborator> Collaborators => Set<Collaborator>();

    public DbSet<MonthlyClose> MonthlyCloses => Set<MonthlyClose>();

    public DbSet<MonthlyCloseParticipant> MonthlyCloseParticipants => Set<MonthlyCloseParticipant>();

    public DbSet<DistributionPayment> DistributionPayments => Set<DistributionPayment>();

    public DbSet<FormDraft> FormDrafts => Set<FormDraft>();

    public DbSet<Chair> Chairs => Set<Chair>();

    public DbSet<ActivityRecord> ActivityRecords => Set<ActivityRecord>();

    public DbSet<UnofficialExpense> UnofficialExpenses => Set<UnofficialExpense>();

    public DbSet<CollaboratorContribution> CollaboratorContributions => Set<CollaboratorContribution>();

    public DbSet<CollaboratorContributionEvent> CollaboratorContributionEvents => Set<CollaboratorContributionEvent>();

    public DbSet<AppNote> Notes => Set<AppNote>();

    public DbSet<FinancialReserve> FinancialReserves => Set<FinancialReserve>();

    public DbSet<FinancialCloseExclusion> FinancialCloseExclusions => Set<FinancialCloseExclusion>();

    public DbSet<MonthlyPurchaseItem> MonthlyPurchaseItems => Set<MonthlyPurchaseItem>();

    public DbSet<Loan> Loans => Set<Loan>();

    public DbSet<LoanInstallment> LoanInstallments => Set<LoanInstallment>();

    public DbSet<LoanPayment> LoanPayments => Set<LoanPayment>();

    public DbSet<AnnualClose> AnnualCloses => Set<AnnualClose>();

    public DbSet<AnnualCarryover> AnnualCarryovers => Set<AnnualCarryover>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PeluqueriaDbContext).Assembly);
    }
}
