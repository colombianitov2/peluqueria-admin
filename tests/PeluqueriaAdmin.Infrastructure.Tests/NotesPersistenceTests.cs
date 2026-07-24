using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Domain.Notes;
using PeluqueriaAdmin.Infrastructure.Notes;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Storage;

namespace PeluqueriaAdmin.Infrastructure.Tests;

public sealed class NotesPersistenceTests
{
    [Fact]
    public async Task NoteSurvivesRealSqliteCloseAndReopen()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "TestData", Guid.NewGuid().ToString("N"));
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTime utc = new(2026, 7, 22, 22, 0, 0, DateTimeKind.Utc);
        try
        {
            ApplicationPaths paths = ApplicationPaths.FromRoot(root);
            paths.EnsureDirectories();
            var firstFactory = new Factory(paths.DatabaseFilePath);
            await new DatabaseInitializer(firstFactory, paths, new FixedTimeProvider(new DateTimeOffset(utc)))
                .InitializeAsync(cancellationToken);
            await new EfNoteRepository(firstFactory).SaveAsync(
                AppNote.Create("Nota exacta\r\ncon dos líneas", utc), cancellationToken);

            SqliteConnection.ClearAllPools();
            AppNote? reloaded = await new EfNoteRepository(new Factory(paths.DatabaseFilePath))
                .GetAsync(cancellationToken);

            Assert.NotNull(reloaded);
            Assert.Equal(AppNote.SingletonId, reloaded.Id);
            Assert.Equal("Nota exacta\r\ncon dos líneas", reloaded.Content);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
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
