using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Persistence;

namespace PeluqueriaAdmin.Infrastructure.Tests;

public sealed class Phase49PersistenceTests
{
    private static readonly DateTime Utc = new(2026, 7, 23, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Phase49_42_PlannedProductPersistsAfterContextRestart()
    {
        await WithDatabase(async (factory, cancellationToken) =>
        {
            await using (PeluqueriaDbContext context = factory.CreateDbContext())
            {
                await context.Database.MigrateAsync(cancellationToken);
                context.MonthlyPurchaseItems.Add(MonthlyPurchaseItem.Create(
                    "Tinte futuro", ProductCategory.ProductForSale, new YearMonth(2026, 8),
                    2, Money.FromDecimal(15m), true, true, Utc, "Plan persistente"));
                await context.SaveChangesAsync(cancellationToken);
            }

            await using PeluqueriaDbContext restarted = factory.CreateDbContext();
            MonthlyPurchaseItem item = await restarted.MonthlyPurchaseItems.SingleAsync(cancellationToken);
            Assert.Equal("Tinte futuro", item.Name);
            Assert.Null(item.ProductId);
        });
    }

    [Fact]
    public async Task Phase49_43_MigrationFromPhase48PreservesLegacyLoanAndCreatesSchedule()
    {
        await WithDatabase(async (factory, cancellationToken) =>
        {
            Guid id = Guid.NewGuid();
            await using (PeluqueriaDbContext context = factory.CreateDbContext())
            {
                string phase48 = context.Database.GetMigrations()
                    .Single(item => item.EndsWith("_Phase48FinancialClosuresReservesLoansInventory", StringComparison.Ordinal));
                await context.GetService<IMigrator>().MigrateAsync(phase48, cancellationToken);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO Loans
                    (Id, Name, InitialBalanceMinorUnits, PendingBalanceMinorUnits, UsualInstallmentMinorUnits,
                     StartDate, Frequency, InstallmentCount, NextDueDate, Description, CreatedUtc, UpdatedUtc, DeletedUtc)
                    VALUES
                    ({id}, {"Préstamo F48"}, {10000L}, {10000L}, {1000L},
                     {"2026-07-01"}, {2}, {10}, {"2026-07-31"}, {"Conservado"}, {Utc.Ticks}, {Utc.Ticks}, {null});
                    """, cancellationToken);
                await context.GetService<IMigrator>().MigrateAsync(cancellationToken: cancellationToken);
            }

            await using PeluqueriaDbContext verified = factory.CreateDbContext();
            var loan = await verified.Loans.SingleAsync(cancellationToken);
            var installment = await verified.LoanInstallments.SingleAsync(cancellationToken);
            Assert.Equal(id, loan.Id);
            Assert.Equal(10_000, loan.ExpectedTotal.MinorUnits);
            Assert.Equal(id, installment.LoanId);
            Assert.Equal(10_000, installment.Amount.MinorUnits);
        });
    }

    [Fact]
    public async Task Phase49_44_SqliteIntegrityCheckReturnsOk()
    {
        await WithDatabase(async (factory, cancellationToken) =>
        {
            await using PeluqueriaDbContext context = factory.CreateDbContext();
            await context.Database.MigrateAsync(cancellationToken);
            await context.Database.OpenConnectionAsync(cancellationToken);
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            Assert.Equal("ok", await command.ExecuteScalarAsync(cancellationToken));
        });
    }

    [Fact]
    public async Task Phase49_45_SqliteForeignKeyCheckHasNoViolations()
    {
        await WithDatabase(async (factory, cancellationToken) =>
        {
            await using PeluqueriaDbContext context = factory.CreateDbContext();
            await context.Database.MigrateAsync(cancellationToken);
            await context.Database.OpenConnectionAsync(cancellationToken);
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "PRAGMA foreign_key_check;";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            Assert.False(await reader.ReadAsync(cancellationToken));
        });
    }

    private static async Task WithDatabase(Func<TestFactory, CancellationToken, Task> test)
    {
        string root = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await test(new TestFactory(Path.Combine(root, "phase49.db")), cancellationToken);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class TestFactory(string path) : IDbContextFactory<PeluqueriaDbContext>
    {
        private readonly DbContextOptions<PeluqueriaDbContext> options = Create(path);
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
