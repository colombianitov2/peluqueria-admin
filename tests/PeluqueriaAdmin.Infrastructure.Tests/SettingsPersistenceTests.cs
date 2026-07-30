using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
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
    public async Task DailyRateChange_PersistsPreviousNewAndDifferenceInAuditEvent()
    {
        string temporaryRoot = CreateTemporaryRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(temporaryRoot);
            paths.EnsureDirectories();
            var factory = new TestDbContextFactory(paths.DatabaseFilePath);
            await new DatabaseInitializer(factory, paths, TimeProvider.System)
                .InitializeAsync(cancellationToken);
            var repository = new EfAdministrationRepository(factory);
            var settingsRepository = new EfSettingsRepository(factory);
            GeneralSettings settings = await settingsRepository.GetAsync(cancellationToken);
            AdministrationData before = await repository.LoadAsync(cancellationToken);
            Assert.Empty(before.DailyRates);
            DateTime initialUtc = DateTime.UtcNow;
            Money initialAmount = Money.FromDecimal(10m);
            settings.Update(
                initialAmount,
                settings.CollaboratorProfit,
                settings.TotalChairs,
                settings.ExportDirectory,
                initialUtc);
            DailyRate previous = DailyRate.Create(
                DateOnly.FromDateTime(initialUtc),
                initialUtc,
                initialAmount,
                initialUtc);
            await repository.SaveSettingsAndDailyRateAsync(
                settings,
                previous,
                cancellationToken);
            DateTime changeUtc = previous.EffectiveFromUtc.AddMinutes(1);
            settings.Update(
                Money.FromDecimal(15m),
                settings.CollaboratorProfit,
                settings.TotalChairs,
                settings.ExportDirectory,
                changeUtc);
            DailyRate replacement = DailyRate.Create(
                previous.EffectiveDate,
                changeUtc,
                Money.FromDecimal(15m),
                changeUtc);

            await repository.SaveSettingsAndDailyRateAsync(
                settings,
                replacement,
                cancellationToken);

            await using PeluqueriaDbContext context =
                await factory.CreateDbContextAsync(cancellationToken);
            FinancialEvent audit = await context.FinancialEvents
                .SingleAsync(item => item.OperationId == replacement.Id, cancellationToken);
            Assert.Equal(previous.Amount!.Value.MinorUnits, audit.PreviousValue?.MinorUnits);
            Assert.Equal(1_500, audit.NewValue?.MinorUnits);
            Assert.Equal(1_500 - previous.Amount.Value.MinorUnits, audit.DifferenceMinorUnits);
            Assert.Equal(changeUtc, (await context.DailyRates
                .SingleAsync(item => item.Id == previous.Id, cancellationToken)).EffectiveToUtc);
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
            Assert.Null(initial.WeeklyUsageFee);
            Assert.False(initial.IsDailyUsageFeeConfirmed);
            Assert.Equal("USD", initial.CurrencyCode.Value);
            await using (PeluqueriaDbContext context = await factory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Empty(await context.DailyRates.ToListAsync(cancellationToken));
            }

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
            Assert.Equal(1_575, reloaded.WeeklyUsageFee?.MinorUnits);
            Assert.True(reloaded.IsDailyUsageFeeConfirmed);
            Assert.Equal(2_550, reloaded.CollaboratorProfit.BasisPoints);
            Assert.Equal(0, reloaded.OptionalSuppliesMonthlyBudget.MinorUnits);
            Assert.Equal(8, reloaded.TotalChairs);
            Assert.Equal("USD", reloaded.CurrencyCode.Value);
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
                await initialContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO Settings
                    (Id, WeeklyUsageFeeMinorUnits, CollaboratorProfitBasisPoints,
                     OptionalSuppliesMonthlyBudgetMinorUnits, TotalChairs, CurrencyCode, CreatedUtc, UpdatedUtc)
                    VALUES (1, {1200L}, {2000}, {0L}, {0}, {"USD"}, {utcNow.Ticks}, {utcNow.Ticks});
                    """, cancellationToken);
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

            await using (PeluqueriaDbContext verification =
                await factory.CreateDbContextAsync(cancellationToken))
            {
                Assert.Equal(1, await verification.Settings.CountAsync(cancellationToken));
                GeneralSettings migratedSettings = await verification.Settings.SingleAsync(cancellationToken);
                Assert.Equal("USD", migratedSettings.CurrencyCode.Value);
                Assert.Equal(1_200, migratedSettings.DailyUsageFee?.MinorUnits);
                Assert.True(migratedSettings.IsDailyUsageFeePendingConfirmation);
                Assert.Empty(await verification.DailyRates.ToListAsync(cancellationToken));
                Assert.Empty(await verification.FinancialEvents
                    .Where(item => item.EntityType == "Tarifa diaria")
                    .ToListAsync(cancellationToken));
                Assert.Contains(
                    await verification.Database.GetAppliedMigrationsAsync(cancellationToken),
                    name => name.EndsWith("_CompleteAdministration", StringComparison.Ordinal));
            }

            DateTimeOffset confirmationTime =
                new(2026, 7, 19, 15, 30, 0, TimeSpan.Zero);
            await new SaveSettingsUseCase(
                    new EfSettingsRepository(factory),
                    repository,
                    new FixedTimeProvider(confirmationTime))
                .ExecuteAsync(
                    new SaveSettingsRequest(
                        12m,
                        20m,
                        string.Empty,
                        ConfirmDailyUsageFee: true),
                    cancellationToken);

            await using PeluqueriaDbContext confirmed =
                await factory.CreateDbContextAsync(cancellationToken);
            GeneralSettings confirmedSettings =
                await confirmed.Settings.SingleAsync(cancellationToken);
            DailyRate confirmedRate =
                await confirmed.DailyRates.SingleAsync(cancellationToken);
            FinancialEvent confirmedEvent = await confirmed.FinancialEvents
                .SingleAsync(
                    item => item.EntityType == "Tarifa diaria",
                    cancellationToken);

            Assert.True(confirmedSettings.IsDailyUsageFeeConfirmed);
            Assert.Equal(1_200, confirmedRate.Amount?.MinorUnits);
            Assert.Equal(confirmationTime.UtcDateTime, confirmedRate.EffectiveFromUtc);
            Assert.Equal(confirmedRate.Id, confirmedEvent.OperationId);
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
    public async Task Migration_PreservesExplicitTenAsActiveOnlyFromUpdateForward()
    {
        string temporaryRoot = CreateTemporaryRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(temporaryRoot);
            paths.EnsureDirectories();
            var factory = new TestDbContextFactory(paths.DatabaseFilePath);
            DateTime utcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

            await using (PeluqueriaDbContext initialContext =
                await factory.CreateDbContextAsync(cancellationToken))
            {
                string initialMigration = initialContext.Database.GetMigrations()
                    .Single(name => name.EndsWith("_InitialSettings", StringComparison.Ordinal));
                await initialContext.GetService<IMigrator>()
                    .MigrateAsync(initialMigration, cancellationToken);
                await initialContext.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO Settings
                    (Id, WeeklyUsageFeeMinorUnits, CollaboratorProfitBasisPoints,
                     OptionalSuppliesMonthlyBudgetMinorUnits, TotalChairs, CurrencyCode, CreatedUtc, UpdatedUtc)
                    VALUES (1, {1000L}, {2000}, {0L}, {0}, {"USD"}, {utcNow.Ticks}, {utcNow.Ticks});
                    """, cancellationToken);
            }

            await new DatabaseInitializer(
                factory,
                paths,
                new FixedTimeProvider(new DateTimeOffset(utcNow)))
                .InitializeAsync(cancellationToken);

            await using PeluqueriaDbContext verification =
                await factory.CreateDbContextAsync(cancellationToken);
            GeneralSettings settings =
                await verification.Settings.SingleAsync(cancellationToken);
            DailyRate rate = await verification.DailyRates.SingleAsync(cancellationToken);

            Assert.Equal(1_000, settings.DailyUsageFee?.MinorUnits);
            Assert.True(settings.IsDailyUsageFeeConfirmed);
            Assert.False(settings.IsDailyUsageFeePendingConfirmation);
            Assert.Equal(1_000, rate.Amount?.MinorUnits);
            Assert.Equal(DateOnly.FromDateTime(DateTime.Now), rate.EffectiveDate);
            Assert.Empty(await verification.DailyCharges.ToListAsync(cancellationToken));
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
        AppContext.BaseDirectory,
        "TestData",
        Guid.NewGuid().ToString("N"));

    private sealed class TestDbContextFactory(string databaseFilePath)
        : IDbContextFactory<PeluqueriaDbContext>
    {
        private readonly DbContextOptions<PeluqueriaDbContext> options = CreateOptions(databaseFilePath);

        private static DbContextOptions<PeluqueriaDbContext> CreateOptions(string databaseFilePath)
        {
            var builder = new DbContextOptionsBuilder<PeluqueriaDbContext>();
            DatabaseConfiguration.Configure(builder, databaseFilePath);
            return builder.Options;
        }

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
