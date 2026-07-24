using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Infrastructure.Persistence;

namespace PeluqueriaAdmin.Infrastructure.Storage;

public sealed class DatabaseBackupService(
    IDbContextFactory<PeluqueriaDbContext> contextFactory,
    ApplicationPaths paths,
    TimeProvider timeProvider)
{
    private const int AutomaticRetention = 90;
    private readonly SemaphoreSlim operationLock = new(1, 1);

    public Task<string> CreateManualAsync(CancellationToken cancellationToken = default) =>
        CreateAsync("manual", cancellationToken);

    public Task<string> CreateBeforeMigrationAsync(CancellationToken cancellationToken = default) =>
        CreateAsync("pre-migration", cancellationToken);

    public Task<string> CreateBeforeRestoreAsync(CancellationToken cancellationToken = default) =>
        CreateAsync("pre-restore", cancellationToken);

    public async Task<string?> CreateAutomaticIfNeededAsync(
        CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            paths.EnsureDirectories();
            if (!File.Exists(paths.DatabaseFilePath))
            {
                return null;
            }

            DateTime latestDataWriteUtc = LatestDatabaseWriteUtc();
            FileInfo? latestBackup = Directory.EnumerateFiles(paths.BackupsDirectory, "auto-*.db")
                .Select(path => new FileInfo(path))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();
            if (latestBackup is not null && latestBackup.LastWriteTimeUtc >= latestDataWriteUtc)
            {
                return null;
            }

            string backup = await CreateCoreAsync("auto", cancellationToken);
            DeleteOldAutomaticBackups();
            return backup;
        }
        finally
        {
            operationLock.Release();
        }
    }

    public async Task<bool> HasPendingSchemaChangesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.DatabaseFilePath))
        {
            return false;
        }

        await using PeluqueriaDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        IEnumerable<string> pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
        IEnumerable<string> applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        return applied.Any() && pending.Any();
    }

    public async Task RestoreAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFilePath);
        string sourcePath = Path.GetFullPath(backupFilePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("La copia seleccionada no existe.", sourcePath);
        }

        await ValidateCompatibleAsync(sourcePath, cancellationToken);
        await CreateBeforeRestoreAsync(cancellationToken);

        string temporaryPath = Path.Combine(paths.DataDirectory, $"restore-{Guid.NewGuid():N}.db");
        string rollbackPath = Path.Combine(paths.DataDirectory, $"rollback-{Guid.NewGuid():N}.db");
        try
        {
            await CopyFileAsync(sourcePath, temporaryPath, cancellationToken);
            await ValidateCompatibleAsync(temporaryPath, cancellationToken);
            await MigrateCandidateAsync(temporaryPath, cancellationToken);
            await ValidateCompatibleAsync(temporaryPath, cancellationToken);

            SqliteConnection.ClearAllPools();
            DeleteSidecars(paths.DatabaseFilePath);
            if (File.Exists(paths.DatabaseFilePath))
            {
                File.Replace(temporaryPath, paths.DatabaseFilePath, rollbackPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, paths.DatabaseFilePath);
            }

            await ValidateCompatibleAsync(paths.DatabaseFilePath, cancellationToken);
        }
        catch
        {
            SqliteConnection.ClearAllPools();
            DeleteSidecars(paths.DatabaseFilePath);
            if (File.Exists(rollbackPath))
            {
                File.Copy(rollbackPath, paths.DatabaseFilePath, overwrite: true);
            }

            throw;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteTemporary(temporaryPath);
            DeleteTemporary(rollbackPath);
            DeleteSidecars(temporaryPath);
            DeleteSidecars(rollbackPath);
        }
    }

    public async Task ValidateCompatibleAsync(
        string databaseFilePath,
        CancellationToken cancellationToken = default)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFilePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand command = connection.CreateCommand();

        command.CommandText = "PRAGMA integrity_check;";
        object? integrity = await command.ExecuteScalarAsync(cancellationToken);
        if (!string.Equals(
                Convert.ToString(integrity, CultureInfo.InvariantCulture),
                "ok",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("La base seleccionada no supera SQLite integrity_check.");
        }

        command.CommandText = "PRAGMA foreign_key_check;";
        await using (SqliteDataReader foreignKeys = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (await foreignKeys.ReadAsync(cancellationToken))
            {
                throw new InvalidDataException("La base seleccionada contiene relaciones inválidas.");
            }
        }

        command.CommandText = """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table'
              AND name IN ('Settings', '__EFMigrationsHistory');
            """;
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        if (Convert.ToInt32(result, CultureInfo.InvariantCulture) != 2)
        {
            throw new InvalidDataException("El archivo no es una base compatible de Peluquería Admin.");
        }

        command.CommandText = """
            SELECT COUNT(*)
            FROM __EFMigrationsHistory
            WHERE MigrationId LIKE '%_InitialSettings';
            """;
        result = await command.ExecuteScalarAsync(cancellationToken);
        if (Convert.ToInt32(result, CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidDataException("La copia no contiene la migración base requerida.");
        }

        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;";
        var applied = new List<string>();
        await using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                applied.Add(reader.GetString(0));
            }
        }

        await using PeluqueriaDbContext modelContext =
            await contextFactory.CreateDbContextAsync(cancellationToken);
        HashSet<string> known = (modelContext.Database.GetMigrations())
            .ToHashSet(StringComparer.Ordinal);
        string[] unknown = applied.Where(item => !known.Contains(item)).ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidDataException(
                $"La copia pertenece a una versión futura no compatible: {string.Join(", ", unknown)}.");
        }
    }

    private async Task<string> CreateAsync(string prefix, CancellationToken cancellationToken)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            return await CreateCoreAsync(prefix, cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }
    }

    private async Task<string> CreateCoreAsync(string prefix, CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        if (!File.Exists(paths.DatabaseFilePath))
        {
            throw new InvalidOperationException("Todavía no existe una base de datos para copiar.");
        }

        string destination = Path.Combine(
            paths.BackupsDirectory,
            $"{prefix}-{timeProvider.GetUtcNow():yyyyMMdd-HHmmssfff}.db");
        string temporary = Path.Combine(
            paths.BackupsDirectory,
            $".{prefix}-{Guid.NewGuid():N}.tmp.db");
        try
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var source = new SqliteConnection(
                    DatabaseConfiguration.CreateConnectionString(paths.DatabaseFilePath));
                using var target = new SqliteConnection(
                    $"Data Source={temporary};Mode=ReadWriteCreate;Pooling=False");
                source.Open();
                target.Open();
                source.BackupDatabase(target);
            }, cancellationToken);
            await ValidateCompatibleAsync(temporary, cancellationToken);
            File.Move(temporary, destination);
            return destination;
        }
        catch
        {
            DeleteTemporary(temporary);
            throw;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteSidecars(temporary);
        }
    }

    private async Task MigrateCandidateAsync(
        string databaseFilePath,
        CancellationToken cancellationToken)
    {
        var builder = new DbContextOptionsBuilder<PeluqueriaDbContext>();
        DatabaseConfiguration.Configure(builder, databaseFilePath);
        await using (var context = new PeluqueriaDbContext(builder.Options))
        {
            await context.Database.MigrateAsync(cancellationToken);
        }

        SqliteConnection.ClearAllPools();
        await using var connection = new SqliteConnection(
            DatabaseConfiguration.CreateConnectionString(databaseFilePath));
        await connection.OpenAsync(cancellationToken);
        await using SqliteCommand checkpoint = connection.CreateCommand();
        checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await checkpoint.ExecuteNonQueryAsync(cancellationToken);
    }

    private DateTime LatestDatabaseWriteUtc()
    {
        string[] candidates =
        [
            paths.DatabaseFilePath,
            paths.DatabaseFilePath + "-wal",
            paths.DatabaseFilePath + "-shm",
        ];
        return candidates
            .Where(File.Exists)
            .Select(path => File.GetLastWriteTimeUtc(path))
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();
    }

    private void DeleteOldAutomaticBackups()
    {
        foreach (FileInfo oldBackup in Directory.EnumerateFiles(paths.BackupsDirectory, "auto-*.db")
                     .Select(path => new FileInfo(path))
                     .OrderByDescending(file => file.CreationTimeUtc)
                     .Skip(AutomaticRetention))
        {
            oldBackup.Delete();
        }
    }

    private static async Task CopyFileAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        await using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using FileStream output = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await input.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }

    private static void DeleteTemporary(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void DeleteSidecars(string databaseFilePath)
    {
        DeleteTemporary(databaseFilePath + "-wal");
        DeleteTemporary(databaseFilePath + "-shm");
    }
}
