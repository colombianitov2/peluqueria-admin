using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.Activity;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Administration;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Settings;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Tests;

public sealed class ActivityAuditTests
{
    [Fact]
    public async Task ConfirmedOperationAndItsActivityHistory_AreCommittedTogether()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(root);
            paths.EnsureDirectories();
            var factory = new Factory(paths.DatabaseFilePath);
            DateTime utc = new(2026, 7, 19, 12, 0, 0, DateTimeKind.Utc);
            var clock = new FixedTimeProvider(new DateTimeOffset(utc));
            await new DatabaseInitializer(factory, paths, clock).InitializeAsync(cancellationToken);
            var repository = new EfAdministrationRepository(factory);
            var service = new AdministrationService(repository, new EfSettingsRepository(factory), clock);
            FinancialEntry entry = FinancialEntry.CreateExpense(
                new DateOnly(2026, 7, 19), "Lavandería", ExpenseCategory.Other,
                Money.FromDecimal(15m), utc, "Descripción conservada");

            await service.AddAsync(entry, cancellationToken);
            AdministrationData afterAdd = await repository.LoadAsync(cancellationToken);
            Assert.Single(afterAdd.FinancialEntries);
            var created = Assert.Single(afterAdd.ActivityRecords);
            Assert.Equal("Gastos", created.Module);
            Assert.Equal("Creación", created.Action);
            Assert.Equal("Descripción conservada", created.Description);

            await service.DeleteAsync(entry, cancellationToken);
            AdministrationData afterDelete = await repository.LoadAsync(cancellationToken);
            Assert.Empty(afterDelete.FinancialEntries);
            Assert.Equal(2, afterDelete.ActivityRecords.Count);
            Assert.Contains(afterDelete.ActivityRecords, item => item.Action == "Eliminación");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SettingsAutosave_RecordsOneConsolidatedChangeAndDoesNotPersistSensitivePath()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(root);
            paths.EnsureDirectories();
            var factory = new Factory(paths.DatabaseFilePath);
            DateTime utc = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
            var clock = new FixedTimeProvider(new DateTimeOffset(utc));
            await new DatabaseInitializer(factory, paths, clock).InitializeAsync(cancellationToken);
            var repository = new EfAdministrationRepository(factory);
            var settingsRepository = new EfSettingsRepository(factory);
            GeneralSettings settings = await settingsRepository.GetAsync(cancellationToken);
            string privatePath = Path.Combine(root, "carpeta-privada");
            settings.Update(Money.FromDecimal(14m), Percentage.FromPercent(25m), settings.TotalChairs,
                privatePath, utc.AddMinutes(1));

            await repository.SaveSettingsAndRateAsync(settings, null, cancellationToken);
            settings.Update(Money.FromDecimal(14m), Percentage.FromPercent(25m), settings.TotalChairs,
                privatePath, utc.AddMinutes(2));
            await repository.SaveSettingsAndRateAsync(settings, null, cancellationToken);

            ActivityRecord activity = Assert.Single((await repository.LoadAsync(cancellationToken)).ActivityRecords);
            Assert.Equal("Ajustes", activity.Module);
            Assert.DoesNotContain(privatePath, activity.Description ?? string.Empty, StringComparison.Ordinal);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
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
