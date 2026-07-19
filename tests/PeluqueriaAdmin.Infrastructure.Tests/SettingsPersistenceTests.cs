using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Administration;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Settings;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Tests;

public sealed class SettingsPersistenceTests
{
    [Fact]
    public async Task InitializationAndRepository_AreIdempotentAndPersistent()
    {
        string temporaryRoot = CreateTemporaryRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(temporaryRoot);
            paths.EnsureDirectories();
            var factory = new TestDbContextFactory(paths.DatabaseFilePath);
            var timeProvider = new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero));
            var initializer = new DatabaseInitializer(factory, paths, timeProvider);

            await initializer.InitializeAsync(cancellationToken);
            await initializer.InitializeAsync(cancellationToken);

            Assert.True(File.Exists(paths.DatabaseFilePath));
            Assert.True(Directory.Exists(paths.BackupsDirectory));
            Assert.True(Directory.Exists(paths.LogsDirectory));

            await using (PeluqueriaDbContext context = await factory.CreateDbContextAsync(cancellationToken))
            {
                IEnumerable<string> appliedMigrations =
                    await context.Database.GetAppliedMigrationsAsync(cancellationToken);
                Assert.Contains(
                    appliedMigrations,
                    migration => migration.EndsWith("_InitialSettings", StringComparison.Ordinal));
                Assert.Equal(1, await context.Settings.CountAsync(cancellationToken));
            }

            var repository = new EfSettingsRepository(factory);
            GeneralSettings initial = await repository.GetAsync(cancellationToken);
            Assert.Equal(1_200, initial.WeeklyUsageFee.MinorUnits);
            Assert.Equal("USD", initial.CurrencyCode.Value);

            DateTime updatedUtc = new(2026, 7, 18, 13, 0, 0, DateTimeKind.Utc);
            initial.Update(
                Money.FromDecimal(15.75m),
                Percentage.FromPercent(25.50m),
                Money.FromDecimal(120.00m),
                8,
                CurrencyCode.From("cop"),
                updatedUtc);
            await repository.SaveAsync(initial, cancellationToken);

            var reloadedRepository = new EfSettingsRepository(factory);
            GeneralSettings reloaded = await reloadedRepository.GetAsync(cancellationToken);
            Assert.Equal(1_575, reloaded.WeeklyUsageFee.MinorUnits);
            Assert.Equal(2_550, reloaded.CollaboratorProfit.BasisPoints);
            Assert.Equal(12_000, reloaded.OptionalSuppliesMonthlyBudget.MinorUnits);
            Assert.Equal(8, reloaded.TotalChairs);
            Assert.Equal("COP", reloaded.CurrencyCode.Value);
            Assert.Equal(updatedUtc, reloaded.UpdatedUtc);
            Assert.Equal(DateTimeKind.Utc, reloaded.CreatedUtc.Kind);
            Assert.Equal(DateTimeKind.Utc, reloaded.UpdatedUtc.Kind);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Database_RejectsASecondGeneralSettingsRow()
    {
        string temporaryRoot = CreateTemporaryRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(temporaryRoot);
            var factory = new TestDbContextFactory(paths.DatabaseFilePath);
            var initializer = new DatabaseInitializer(factory, paths, TimeProvider.System);
            await initializer.InitializeAsync(cancellationToken);

            await using PeluqueriaDbContext context = await factory.CreateDbContextAsync(cancellationToken);
            GeneralSettings duplicate = GeneralSettings.CreateDefault(DateTime.UtcNow);
            context.Settings.Add(duplicate);

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(cancellationToken));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task CompleteMigration_PreservesInitialSettingsAndPersistsOperationalData()
    {
        string temporaryRoot = CreateTemporaryRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(temporaryRoot);
            paths.EnsureDirectories();
            var factory = new TestDbContextFactory(paths.DatabaseFilePath);
            DateTime utcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

            await using (PeluqueriaDbContext initialContext = await factory.CreateDbContextAsync(cancellationToken))
            {
                string initialMigration = initialContext.Database.GetMigrations()
                    .Single(name => name.EndsWith("_InitialSettings", StringComparison.Ordinal));
                IMigrator migrator = initialContext.GetService<IMigrator>();
                await migrator.MigrateAsync(initialMigration, cancellationToken);
                initialContext.Settings.Add(GeneralSettings.CreateDefault(utcNow));
                await initialContext.SaveChangesAsync(cancellationToken);
            }

            var initializer = new DatabaseInitializer(
                factory,
                paths,
                new FixedTimeProvider(new DateTimeOffset(utcNow)));
            await initializer.InitializeAsync(cancellationToken);

            var repository = new EfAdministrationRepository(factory);
            LocalUsePerson person = LocalUsePerson.Create("Ana", new DateOnly(2026, 7, 1), null, utcNow);
            Product product = Product.Create("Agua", ProductCategory.ProductForSale, "unidad", utcNow);
            await repository.SaveAsync([person, product], [], cancellationToken);

            AdministrationData loaded = await repository.LoadAsync(cancellationToken);
            Assert.Single(loaded.LocalUsePeople);
            Assert.Single(loaded.Products);

            person.MarkDeleted(utcNow.AddMinutes(1));
            await repository.SaveAsync([], [person], cancellationToken);
            AdministrationData afterDelete = await repository.LoadAsync(cancellationToken);
            Assert.Empty(afterDelete.LocalUsePeople);

            await using PeluqueriaDbContext verification = await factory.CreateDbContextAsync(cancellationToken);
            Assert.Equal(1, await verification.Settings.CountAsync(cancellationToken));
            Assert.Equal("USD", (await verification.Settings.SingleAsync(cancellationToken)).CurrencyCode.Value);
            Assert.Contains(
                await verification.Database.GetAppliedMigrationsAsync(cancellationToken),
                name => name.EndsWith("_CompleteAdministration", StringComparison.Ordinal));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static string CreateTemporaryRoot() => Path.Combine(
        Path.GetTempPath(),
        "PeluqueriaAdmin.Tests",
        Guid.NewGuid().ToString("N"));

    private sealed class TestDbContextFactory(string databaseFilePath)
        : IDbContextFactory<PeluqueriaDbContext>
    {
        private readonly DbContextOptions<PeluqueriaDbContext> options =
            new DbContextOptionsBuilder<PeluqueriaDbContext>()
                .UseSqlite(DatabaseConfiguration.CreateConnectionString(databaseFilePath))
                .Options;

        public PeluqueriaDbContext CreateDbContext() => new(options);

        public Task<PeluqueriaDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
