using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Administration;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Settings;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Tests;

public sealed class DataManagementTests
{
    [Fact]
    public async Task ManualBackupAndRestore_RecoverPreviousDatabaseState()
    {
        string temporaryRoot = CreateTemporaryRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            (ApplicationPaths paths, TestDbContextFactory factory, FixedTimeProvider timeProvider) =
                CreateDependencies(temporaryRoot);
            var backupService = new DatabaseBackupService(factory, paths, timeProvider);
            var initializer = new DatabaseInitializer(factory, paths, timeProvider, backupService);
            await initializer.InitializeAsync(cancellationToken);

            var settingsRepository = new EfSettingsRepository(factory);
            GeneralSettings settings = await settingsRepository.GetAsync(cancellationToken);
            Assert.Equal(1_200, settings.WeeklyUsageFee.MinorUnits);

            string backupPath = await backupService.CreateManualAsync(cancellationToken);
            Assert.True(File.Exists(backupPath));

            settings.Update(
                Money.FromDecimal(99.99m),
                settings.CollaboratorProfit,
                settings.OptionalSuppliesMonthlyBudget,
                settings.TotalChairs,
                settings.CurrencyCode,
                timeProvider.GetUtcNow().UtcDateTime.AddMinutes(1));
            await settingsRepository.SaveAsync(settings, cancellationToken);
            Assert.Equal(
                9_999,
                (await settingsRepository.GetAsync(cancellationToken)).WeeklyUsageFee.MinorUnits);

            await backupService.RestoreAsync(backupPath, cancellationToken);

            GeneralSettings restored = await new EfSettingsRepository(factory).GetAsync(cancellationToken);
            Assert.Equal(1_200, restored.WeeklyUsageFee.MinorUnits);
            Assert.Single(Directory.EnumerateFiles(paths.BackupsDirectory, "pre-restore-*.db"));
        }
        finally
        {
            Cleanup(temporaryRoot);
        }
    }

    [Fact]
    public async Task Export_CreatesFiveUtf8CsvFilesWithHeaders()
    {
        string temporaryRoot = CreateTemporaryRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            (ApplicationPaths paths, TestDbContextFactory factory, FixedTimeProvider timeProvider) =
                CreateDependencies(temporaryRoot);
            var backupService = new DatabaseBackupService(factory, paths, timeProvider);
            var initializer = new DatabaseInitializer(factory, paths, timeProvider, backupService);
            await initializer.InitializeAsync(cancellationToken);
            var service = new CsvDataManagementService(
                new EfAdministrationRepository(factory),
                new EfSettingsRepository(factory),
                backupService,
                paths,
                timeProvider);

            IReadOnlyList<string> files = await service.ExportAsync(cancellationToken);

            Assert.Equal(5, files.Count);
            Assert.All(files, file => Assert.True(File.Exists(file)));
            Assert.Contains(files, file => Path.GetFileName(file).StartsWith("resumen-mensual-", StringComparison.Ordinal));
            Assert.Contains(files, file => Path.GetFileName(file).StartsWith("balance-anual-", StringComparison.Ordinal));
            Assert.Contains(files, file => Path.GetFileName(file).StartsWith("flujo-caja-", StringComparison.Ordinal));
            Assert.Contains(files, file => Path.GetFileName(file).StartsWith("inventario-actual-", StringComparison.Ordinal));
            Assert.Contains(files, file => Path.GetFileName(file).StartsWith("deudas-uso-local-", StringComparison.Ordinal));

            foreach (string file in files)
            {
                byte[] bytes = await File.ReadAllBytesAsync(file, cancellationToken);
                Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
                Assert.StartsWith("\"", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            }
        }
        finally
        {
            Cleanup(temporaryRoot);
        }
    }

    private static (ApplicationPaths, TestDbContextFactory, FixedTimeProvider) CreateDependencies(
        string temporaryRoot)
    {
        ApplicationPaths paths = ApplicationPaths.FromRoot(temporaryRoot);
        paths.EnsureDirectories();
        var factory = new TestDbContextFactory(paths.DatabaseFilePath);
        var timeProvider = new FixedTimeProvider(
            new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero));
        return (paths, factory, timeProvider);
    }

    private static string CreateTemporaryRoot() => Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        Guid.NewGuid().ToString("N"));

    private static void Cleanup(string temporaryRoot)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(temporaryRoot))
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

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
