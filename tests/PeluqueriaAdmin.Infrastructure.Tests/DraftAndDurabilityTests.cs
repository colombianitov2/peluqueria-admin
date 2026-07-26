using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Drafts;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Administration;
using PeluqueriaAdmin.Infrastructure.Drafts;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Tests;

public sealed class DraftAndDurabilityTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 19, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Draft_IsRecoveredAfterRestartAndRemovedAtomicallyWithCompletedOperation()
    {
        string root = CreateRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            (ApplicationPaths paths, TestDbContextFactory factory) = CreateDependencies(root);
            await new DatabaseInitializer(factory, paths, TimeProvider.System).InitializeAsync(cancellationToken);
            const string draftKey = "Otros ingresos:Registrar ingreso:new";
            var store = new EfFormDraftStore(factory);
            await store.UpsertAsync(FormDraft.Create(
                draftKey,
                "Otros ingresos",
                "Registrar ingreso",
                "{\"concepto\":\"Servicio externo\"}",
                null,
                false,
                UtcNow), cancellationToken);

            FormDraft? recovered = await new EfFormDraftStore(factory).FindAsync(draftKey, cancellationToken);

            Assert.NotNull(recovered);
            Assert.Contains("Servicio externo", recovered.PayloadJson, StringComparison.Ordinal);
            var repository = new EfAdministrationRepository(factory);
            await repository.SaveCompletingDraftAsync(
                [FinancialEntry.CreateIncome(
                    new DateOnly(2026, 7, 19), "Servicio externo", Money.FromDecimal(25m), UtcNow)],
                [],
                draftKey,
                cancellationToken);

            Assert.Null(await store.FindAsync(draftKey, cancellationToken));
            Assert.Single((await repository.LoadAsync(cancellationToken)).FinancialEntries);
        }
        finally
        {
            Cleanup(root);
        }
    }

    [Fact]
    public async Task EveryConnection_UsesApprovedSqliteDurabilityPolicy()
    {
        string root = CreateRoot();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            (ApplicationPaths paths, TestDbContextFactory factory) = CreateDependencies(root);
            await new DatabaseInitializer(factory, paths, TimeProvider.System).InitializeAsync(cancellationToken);
            await using PeluqueriaDbContext context = await factory.CreateDbContextAsync(cancellationToken);
            await context.Database.OpenConnectionAsync(cancellationToken);
            DbConnection connection = context.Database.GetDbConnection();

            Assert.Equal(1L, await ScalarAsync<long>(connection, "PRAGMA foreign_keys;", cancellationToken));
            Assert.Equal("wal", await ScalarAsync<string>(connection, "PRAGMA journal_mode;", cancellationToken));
            Assert.Equal(2L, await ScalarAsync<long>(connection, "PRAGMA synchronous;", cancellationToken));
            Assert.Equal(5_000L, await ScalarAsync<long>(connection, "PRAGMA busy_timeout;", cancellationToken));
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static async Task<T> ScalarAsync<T>(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = commandText;
        object? value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is null or DBNull)
        {
            throw new InvalidOperationException($"SQLite no devolvió un valor para {commandText}");
        }

        return (T)Convert.ChangeType(value, typeof(T), System.Globalization.CultureInfo.InvariantCulture)!;
    }

    private static (ApplicationPaths Paths, TestDbContextFactory Factory) CreateDependencies(string root)
    {
        ApplicationPaths paths = ApplicationPaths.FromRoot(root);
        paths.EnsureDirectories();
        return (paths, new TestDbContextFactory(paths.DatabaseFilePath));
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

    private sealed class TestDbContextFactory(string databasePath)
        : IDbContextFactory<PeluqueriaDbContext>
    {
        private readonly DbContextOptions<PeluqueriaDbContext> options = CreateOptions(databasePath);

        public PeluqueriaDbContext CreateDbContext() => new(options);

        public Task<PeluqueriaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());

        private static DbContextOptions<PeluqueriaDbContext> CreateOptions(string databasePath)
        {
            var builder = new DbContextOptionsBuilder<PeluqueriaDbContext>();
            DatabaseConfiguration.Configure(builder, databasePath);
            return builder.Options;
        }
    }
}
