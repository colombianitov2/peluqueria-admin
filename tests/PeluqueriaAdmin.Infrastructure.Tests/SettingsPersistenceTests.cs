using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Collaborators;
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
    public async Task FreshInitialization_RemainsUnconfiguredWithoutRateOrHistoryAfterRestart()
    {
        string temporaryRoot = CreateTemporaryRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(temporaryRoot);
            var factory = new TestDbContextFactory(paths.DatabaseFilePath);
            var timeProvider = new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));
            var initializer = new DatabaseInitializer(factory, paths, timeProvider);

            await initializer.InitializeAsync(cancellationToken);
            await initializer.InitializeAsync(cancellationToken);

            await using PeluqueriaDbContext context =
                await factory.CreateDbContextAsync(cancellationToken);
            GeneralSettings settings = await context.Settings.SingleAsync(cancellationToken);
            Assert.Null(settings.DailyUsageFee);
            Assert.Null(settings.CollaboratorProfit);
            Assert.Equal(string.Empty, settings.ExportDirectory);
            Assert.Empty(await context.DailyRates.ToArrayAsync(cancellationToken));
            Assert.Empty(await context.FinancialEvents.ToArrayAsync(cancellationToken));
            Assert.Empty(await context.ActivityRecords.ToArrayAsync(cancellationToken));

            Collaborator collaborator = Collaborator.Create(
                "Sin porcentaje",
                new DateOnly(2026, 7, 29),
                null,
                timeProvider.GetUtcNow().UtcDateTime);
            context.Collaborators.Add(collaborator);
            await context.SaveChangesAsync(cancellationToken);
            context.ChangeTracker.Clear();

            Collaborator persisted = await context.Collaborators.SingleAsync(cancellationToken);
            Assert.Null(persisted.ProfitShareBasisPoints);
            Assert.Null(persisted.FundParticipationBasisPoints);
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
    public async Task ExplicitZero_IsConfiguredAndCreatesTheFirstDailyRate()
    {
        string temporaryRoot = CreateTemporaryRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(temporaryRoot);
            var factory = new TestDbContextFactory(paths.DatabaseFilePath);
            var timeProvider = new FixedTimeProvider(
                new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero));
            await new DatabaseInitializer(factory, paths, timeProvider)
                .InitializeAsync(cancellationToken);
            var settingsRepository = new EfSettingsRepository(factory);
            var administrationRepository = new EfAdministrationRepository(factory);
            var useCase = new SaveSettingsUseCase(
                settingsRepository,
                administrationRepository,
                timeProvider);

            SettingsDto saved = await useCase.ExecuteAsync(
                new SaveSettingsRequest(0m, 0m, string.Empty),
                cancellationToken);

            Assert.Equal(0m, saved.DailyUsageFee);
            Assert.Equal(0m, saved.CollaboratorProfitPercent);
            await using PeluqueriaDbContext context =
                await factory.CreateDbContextAsync(cancellationToken);
            DailyRate rate = Assert.Single(
                await context.DailyRates.ToArrayAsync(cancellationToken));
            Assert.Equal(0, rate.Amount.MinorUnits);
            Assert.Single(await context.FinancialEvents
                .Where(item => item.OperationId == rate.Id)
                .ToArrayAsync(cancellationToken));

            GeneralSettings reloaded = await new EfSettingsRepository(factory)
                .GetAsync(cancellationToken);
            Assert.Equal(0, reloaded.RequireDailyUsageFee().MinorUnits);
            Assert.Equal(0, reloaded.RequireCollaboratorProfit().BasisPoints);
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
    public async Task ClearingDailyRate_ClosesItsVigencyAndDoesNotCoverTheUnconfiguredGap()
    {
        string temporaryRoot = CreateTemporaryRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(temporaryRoot);
            var factory = new TestDbContextFactory(paths.DatabaseFilePath);
            var timeProvider = new MutableTimeProvider(
                new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero));
            await new DatabaseInitializer(factory, paths, timeProvider)
                .InitializeAsync(cancellationToken);
            var useCase = new SaveSettingsUseCase(
                new EfSettingsRepository(factory),
                new EfAdministrationRepository(factory),
                timeProvider);

            await useCase.ExecuteAsync(
                new SaveSettingsRequest(12m, 20m, string.Empty),
                cancellationToken);
            timeProvider.AdvanceDays(2);
            await useCase.ExecuteAsync(
                new SaveSettingsRequest(null, 20m, string.Empty),
                cancellationToken);
            timeProvider.AdvanceDays(2);
            await useCase.ExecuteAsync(
                new SaveSettingsRequest(15m, 20m, string.Empty),
                cancellationToken);

            await using PeluqueriaDbContext context =
                await factory.CreateDbContextAsync(cancellationToken);
            DailyRate[] rates = await context.DailyRates
                .OrderBy(item => item.EffectiveFromUtc)
                .ToArrayAsync(cancellationToken);
            Assert.Equal(2, rates.Length);
            Assert.Equal(new DateTime(2026, 7, 3, 12, 0, 0, DateTimeKind.Utc), rates[0].EffectiveToUtc);
            Assert.Equal(new DateOnly(2026, 7, 3), rates[0].EffectiveToDateExclusive);
            Assert.Equal(new DateTime(2026, 7, 5, 12, 0, 0, DateTimeKind.Utc), rates[1].EffectiveFromUtc);
            Assert.DoesNotContain(
                rates,
                item => item.EffectiveFromUtc <= new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc)
                    && (!item.EffectiveToUtc.HasValue
                        || new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc) < item.EffectiveToUtc.Value));
            Assert.Contains(
                await context.FinancialEvents.ToArrayAsync(cancellationToken),
                item => item.EventType == "Tarifa diaria desconfigurada"
                    && item.NewValue == null);
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
    public async Task DailyRateChange_PersistsPreviousNewAndDifferenceInAuditEvent()
    {
        string temporaryRoot = CreateTemporaryRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(temporaryRoot);
            paths.EnsureDirectories();
            var factory = new TestDbContextFactory(paths.DatabaseFilePath);
            await new ConfiguredDatabaseInitializer(factory, paths, TimeProvider.System)
                .InitializeAsync(cancellationToken);
            var repository = new EfAdministrationRepository(factory);
            var settingsRepository = new EfSettingsRepository(factory);
            GeneralSettings settings = await settingsRepository.GetAsync(cancellationToken);
            AdministrationData before = await repository.LoadAsync(cancellationToken);
            DailyRate previous;
            if (before.DailyRates.Count == 0)
            {
                DateTime initialUtc = DateTime.UtcNow;
                previous = DailyRate.Create(
                    DateOnly.FromDateTime(initialUtc),
                    initialUtc,
                    settings.RequireDailyUsageFee(),
                    initialUtc);
                await repository.SaveSettingsAndDailyRateAsync(
                    settings,
                    previous,
                    cancellationToken);
            }
            else
            {
                previous = Assert.Single(before.DailyRates);
            }
            DateTime changeUtc = previous.EffectiveFromUtc.AddMinutes(1);
            settings.Update(
                Money.FromDecimal(15m),
                settings.RequireCollaboratorProfit(),
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
            Assert.Equal(previous.Amount.MinorUnits, audit.PreviousValue?.MinorUnits);
            Assert.Equal(1_500, audit.NewValue?.MinorUnits);
            Assert.Equal(1_500 - previous.Amount.MinorUnits, audit.DifferenceMinorUnits);
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
            var initializer = new ConfiguredDatabaseInitializer(factory, paths, timeProvider);

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
            Assert.Equal(1_200, initial.WeeklyUsageFee!.Value.MinorUnits);
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
            Assert.Equal(1_575, reloaded.WeeklyUsageFee!.Value.MinorUnits);
            Assert.Equal(2_550, reloaded.CollaboratorProfit!.Value.BasisPoints);
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
            var initializer = new ConfiguredDatabaseInitializer(factory, paths, TimeProvider.System);
            await initializer.InitializeAsync(cancellationToken);

            await using PeluqueriaDbContext context = await factory.CreateDbContextAsync(cancellationToken);
            GeneralSettings duplicate = GeneralSettings.CreateConfigured(Money.FromDecimal(12m), Percentage.FromPercent(20m), DateTime.UtcNow);
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

            var initializer = new ConfiguredDatabaseInitializer(
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
            GeneralSettings preserved = await verification.Settings.SingleAsync(cancellationToken);
            Assert.Equal("USD", preserved.CurrencyCode.Value);
            Assert.Equal(1_200, preserved.RequireDailyUsageFee().MinorUnits);
            Assert.Equal(2_000, preserved.RequireCollaboratorProfit().BasisPoints);
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

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset current = utcNow;
        public override DateTimeOffset GetUtcNow() => current;
        public void AdvanceDays(int days) => current = current.AddDays(days);
    }
}
