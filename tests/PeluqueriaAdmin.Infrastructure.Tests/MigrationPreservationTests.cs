using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Administration;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Settings;

namespace PeluqueriaAdmin.Infrastructure.Tests;

public sealed class MigrationPreservationTests
{
    [Fact]
    public async Task Alpha1SchemaToPersistentDrafts_PreservesEveryAdministrationTable()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var factory = new TestFactory(Path.Combine(root, "alpha1.db"));
            DateTime utc = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);
            await using (PeluqueriaDbContext context = factory.CreateDbContext())
            {
                string alpha1 = context.Database.GetMigrations().Single(x => x.EndsWith("_CompleteAdministration", StringComparison.Ordinal));
                await context.GetService<IMigrator>().MigrateAsync(alpha1, cancellationToken);
                context.Settings.Add(GeneralSettings.CreateDefault(utc));
                await context.SaveChangesAsync(cancellationToken);
            }

            var repository = new EfAdministrationRepository(factory);
            var service = new AdministrationService(repository, new EfSettingsRepository(factory), new FixedClock(utc));
            var person = LocalUsePerson.Create("Persona", new DateOnly(2026, 7, 1), null, utc);
            await service.AddLocalUsePersonAsync(person, new DateOnly(2026, 7, 8), cancellationToken);
            await service.RegisterLocalUsePaymentAsync(person.Id, new DateOnly(2026, 7, 8), Money.FromDecimal(5), cancellationToken);
            var product = Product.Create("Producto", ProductCategory.ProductForSale, "unidad", utc);
            await service.AddProductAsync(product, cancellationToken);
            await service.AddInventoryMovementAsync(InventoryMovement.Initial(product.Id, new DateOnly(2026, 7, 1), Quantity.Positive(5), Money.FromDecimal(50), utc), cancellationToken);
            await service.AddAsync(MonthlyRestockPlan.Create(product.Id, new YearMonth(2026, 8), Quantity.NonNegative(2), utc), cancellationToken);
            await service.AddAsync(FinancialEntry.CreateIncome(new DateOnly(2026, 7, 2), "Ingreso", Money.FromDecimal(20), utc), cancellationToken);
            var obligation = Obligation.Create("Servicio", ObligationType.Service, new DateOnly(2026, 7, 10), Money.FromDecimal(10), RecurrenceFrequency.None, utc);
            await service.AddObligationAsync(obligation, new DateOnly(2026, 7, 31), cancellationToken);
            await service.AddAsync(ObligationPayment.Create(obligation.Id, new DateOnly(2026, 7, 10), Money.FromDecimal(10), utc), cancellationToken);
            await service.AddAsync(MaintenanceRecord.Create("Silla", "Preventivo", new DateOnly(2026, 8, 1), Money.FromDecimal(5), null, null, utc), cancellationToken);
            var collaborator = Collaborator.Create("Colaborador", new DateOnly(2026, 1, 1), null, utc);
            await service.AddAsync(collaborator, cancellationToken);
            var close = await service.CloseMonthAsync(new YearMonth(2026, 7), new MonthlySummaryInput(1000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), Percentage.FromPercent(20), [collaborator.Id], cancellationToken);
            await service.RegisterDistributionPaymentAsync(close.Participants.Single().Id, new DateOnly(2026, 7, 31), Money.FromDecimal(2), cancellationToken);

            await using (PeluqueriaDbContext context = factory.CreateDbContext())
            {
                await context.GetService<IMigrator>().MigrateAsync(cancellationToken: cancellationToken);
                Assert.Equal(1, await context.Settings.CountAsync(cancellationToken));
                Assert.True(await context.LocalUsePeople.AnyAsync(cancellationToken));
                Assert.True(await context.WeeklyRates.AnyAsync(cancellationToken));
                Assert.True(await context.WeeklyCharges.AnyAsync(cancellationToken));
                Assert.True(await context.LocalUsePayments.AnyAsync(cancellationToken));
                Assert.True(await context.Products.AnyAsync(cancellationToken));
                Assert.True(await context.InventoryMovements.AnyAsync(cancellationToken));
                Assert.True(await context.RestockPlans.AnyAsync(cancellationToken));
                Assert.True(await context.FinancialEntries.AnyAsync(cancellationToken));
                Assert.True(await context.Obligations.AnyAsync(cancellationToken));
                Assert.True(await context.ObligationPayments.AnyAsync(cancellationToken));
                Assert.True(await context.MaintenanceRecords.AnyAsync(cancellationToken));
                Assert.True(await context.Collaborators.AnyAsync(cancellationToken));
                Assert.True(await context.MonthlyCloses.AnyAsync(cancellationToken));
                Assert.True(await context.MonthlyCloseParticipants.AnyAsync(cancellationToken));
                Assert.True(await context.DistributionPayments.AnyAsync(cancellationToken));
                Assert.Equal(0, await context.FormDrafts.CountAsync(cancellationToken));
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class FixedClock(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc);
    }

    private sealed class TestFactory(string path) : IDbContextFactory<PeluqueriaDbContext>
    {
        private readonly DbContextOptions<PeluqueriaDbContext> options = Create(path);
        public PeluqueriaDbContext CreateDbContext() => new(options);
        public Task<PeluqueriaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
        private static DbContextOptions<PeluqueriaDbContext> Create(string path)
        {
            var builder = new DbContextOptionsBuilder<PeluqueriaDbContext>();
            DatabaseConfiguration.Configure(builder, path);
            return builder.Options;
        }
    }
}
