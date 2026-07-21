using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Administration;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Settings;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Tests;

public sealed class AdvancePaymentPersistenceTests
{
    [Fact]
    public async Task WorkerEntryDatesAndZeroOrFortyEightDollarDebtSurviveRealSqliteRestart()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTime utc = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
        DateOnly today = new(2026, 7, 20);
        var clock = new FixedTimeProvider(new DateTimeOffset(utc));
        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(root);
            paths.EnsureDirectories();
            var firstFactory = new Factory(paths.DatabaseFilePath);
            await new DatabaseInitializer(firstFactory, paths, clock).InitializeAsync(cancellationToken);
            var firstService = new AdministrationService(
                new EfAdministrationRepository(firstFactory),
                new EfSettingsRepository(firstFactory),
                clock);
            LocalUsePerson current = LocalUsePerson.Create("Ingreso actual", today, null, utc);
            LocalUsePerson historical = LocalUsePerson.Create(
                "Ingreso histórico", new DateOnly(2026, 6, 16), null, utc);
            await firstService.AddLocalUsePersonAsync(current, today, cancellationToken);
            await firstService.AddLocalUsePersonAsync(historical, today, cancellationToken);

            SqliteConnection.ClearAllPools();
            AdministrationData reloaded = await new EfAdministrationRepository(
                new Factory(paths.DatabaseFilePath)).LoadAsync(cancellationToken);
            LocalUsePerson reloadedCurrent = reloaded.LocalUsePeople.Single(item => item.Id == current.Id);
            LocalUsePerson reloadedHistorical = reloaded.LocalUsePeople.Single(item => item.Id == historical.Id);

            Assert.Equal(today, reloadedCurrent.EntryDate);
            Assert.Equal(new DateOnly(2026, 6, 16), reloadedHistorical.EntryDate);
            Assert.Equal(0, WeeklyChargeCalculator.CalculateDebt(
                reloaded.WeeklyCharges.Where(item => item.PersonId == current.Id),
                reloaded.LocalUsePayments.Where(item => item.PersonId == current.Id), today).MinorUnits);
            Assert.Equal(4_800, WeeklyChargeCalculator.CalculateDebt(
                reloaded.WeeklyCharges.Where(item => item.PersonId == historical.Id),
                reloaded.LocalUsePayments.Where(item => item.PersonId == historical.Id), today).MinorUnits);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task AdvancePayment_IsPersistedOnceAndRecalculatedAfterRealSqliteRestart()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTime utc = new(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc);
        DateOnly today = DateOnly.FromDateTime(utc);
        var clock = new FixedTimeProvider(new DateTimeOffset(utc));
        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(root);
            paths.EnsureDirectories();
            var firstFactory = new Factory(paths.DatabaseFilePath);
            await new DatabaseInitializer(firstFactory, paths, clock).InitializeAsync(cancellationToken);
            var firstService = new AdministrationService(
                new EfAdministrationRepository(firstFactory),
                new EfSettingsRepository(firstFactory),
                clock);
            LocalUsePerson worker = LocalUsePerson.Create("Ana", today, null, utc);
            await firstService.AddLocalUsePersonAsync(worker, today, cancellationToken);
            await firstService.RegisterLocalUsePaymentAsync(
                worker.Id, today, Money.FromDecimal(1000m), cancellationToken,
                description: "Pago anticipado");

            SqliteConnection.ClearAllPools();
            var restartedFactory = new Factory(paths.DatabaseFilePath);
            var restartedRepository = new EfAdministrationRepository(restartedFactory);
            AdministrationData reloaded = await restartedRepository.LoadAsync(cancellationToken);
            LocalUsePayment payment = Assert.Single(reloaded.LocalUsePayments);
            LocalUsePerson reloadedWorker = Assert.Single(reloaded.LocalUsePeople);
            WorkerAccountBalance account = WeeklyChargeCalculator.CalculateAccount(
                reloadedWorker,
                reloaded.WeeklyCharges,
                reloaded.LocalUsePayments,
                reloaded.WeeklyRates,
                today);

            Assert.Equal(worker.Id, payment.PersonId);
            Assert.Equal(100_000, payment.Amount.MinorUnits);
            Assert.Equal("Pago anticipado", payment.Description);
            Assert.Equal(100_000, account.Credit.MinorUnits);
            Assert.Equal(800, account.NextRequiredPaymentAmount?.MinorUnits);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class Factory(string databasePath) : IDbContextFactory<PeluqueriaDbContext>
    {
        private readonly DbContextOptions<PeluqueriaDbContext> options = Create(databasePath);

        public PeluqueriaDbContext CreateDbContext() => new(options);

        public Task<PeluqueriaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());

        private static DbContextOptions<PeluqueriaDbContext> Create(string path)
        {
            var builder = new DbContextOptionsBuilder<PeluqueriaDbContext>();
            DatabaseConfiguration.Configure(builder, path);
            return builder.Options;
        }
    }
}
