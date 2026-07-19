using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Drafts;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PeluqueriaDbContext).Assembly);
    }
}
