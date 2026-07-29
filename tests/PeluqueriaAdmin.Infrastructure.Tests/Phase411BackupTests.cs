using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Tests;

public sealed class Phase411BackupTests
{
    [Fact]
    public async Task Restore_RejectsCorruptionBeforeReplacingTheActiveDatabase()
    {
        string root = CreateRoot();
        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(root);
            var factory = new Factory(paths.DatabaseFilePath);
            var service = new DatabaseBackupService(factory, paths, TimeProvider.System);
            await new ConfiguredDatabaseInitializer(factory, paths, TimeProvider.System, service)
                .InitializeAsync(TestContext.Current.CancellationToken);
            string before = await service.CreateManualAsync(TestContext.Current.CancellationToken);
            string corrupt = Path.Combine(root, "corrupt.db");
            await File.WriteAllTextAsync(
                corrupt,
                "esto no es sqlite",
                TestContext.Current.CancellationToken);

            await Assert.ThrowsAnyAsync<Exception>(() =>
                service.RestoreAsync(corrupt, TestContext.Current.CancellationToken));
            await service.ValidateCompatibleAsync(
                paths.DatabaseFilePath,
                TestContext.Current.CancellationToken);
            Assert.True(File.Exists(before));
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task Validation_RejectsAMigrationUnknownToTheInstalledVersion()
    {
        string root = CreateRoot();
        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(root);
            var factory = new Factory(paths.DatabaseFilePath);
            var service = new DatabaseBackupService(factory, paths, TimeProvider.System);
            await new ConfiguredDatabaseInitializer(factory, paths, TimeProvider.System, service)
                .InitializeAsync(TestContext.Current.CancellationToken);
            string candidate = await service.CreateManualAsync(TestContext.Current.CancellationToken);
            await using (var connection = new SqliteConnection($"Data Source={candidate};Pooling=False"))
            {
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await using SqliteCommand command = connection.CreateCommand();
                command.CommandText = "INSERT INTO __EFMigrationsHistory(MigrationId, ProductVersion) VALUES ('99999999999999_FutureMigration', '99.0.0');";
                await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.ValidateCompatibleAsync(candidate, TestContext.Current.CancellationToken));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static string CreateRoot() => Path.Combine(
        AppContext.BaseDirectory,
        "TestData",
        Guid.NewGuid().ToString("N"));

    private static void Cleanup(string root)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class Factory(string databaseFilePath)
        : IDbContextFactory<PeluqueriaDbContext>
    {
        private readonly DbContextOptions<PeluqueriaDbContext> options = CreateOptions(databaseFilePath);

        public PeluqueriaDbContext CreateDbContext() => new(options);

        public Task<PeluqueriaDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());

        private static DbContextOptions<PeluqueriaDbContext> CreateOptions(string databaseFilePath)
        {
            var builder = new DbContextOptionsBuilder<PeluqueriaDbContext>();
            DatabaseConfiguration.Configure(builder, databaseFilePath);
            return builder.Options;
        }
    }
}
