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
    private const int AutomaticRetention = 30;

    public Task<string> CreateManualAsync(CancellationToken cancellationToken = default) =>
        CreateAsync("manual", cancellationToken);

    public Task<string> CreateBeforeMigrationAsync(CancellationToken cancellationToken = default) =>
        CreateAsync("pre-migration", cancellationToken);

    public Task<string> CreateBeforeRestoreAsync(CancellationToken cancellationToken = default) =>
        CreateAsync("pre-restore", cancellationToken);

    public async Task<string?> CreateAutomaticIfNeededAsync(CancellationToken cancellationToken = default)
    {
        paths.EnsureDirectories();
        if (!File.Exists(paths.DatabaseFilePath))
        {
            return null;
        }

        DateTime todayUtc = timeProvider.GetUtcNow().UtcDateTime.Date;
        FileInfo? todayBackup = Directory.EnumerateFiles(paths.BackupsDirectory, "auto-*.db")
            .Select(path => new FileInfo(path))
            .Where(file => file.CreationTimeUtc.Date == todayUtc)
            .OrderByDescending(file => file.CreationTimeUtc)
            .FirstOrDefault();
        if (todayBackup is not null)
        {
            return null;
        }

        FileInfo database = new(paths.DatabaseFilePath);
        FileInfo? latest = Directory.EnumerateFiles(paths.BackupsDirectory, "auto-*.db")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();
        if (latest is not null && latest.LastWriteTimeUtc >= database.LastWriteTimeUtc)
        {
            return null;
        }

        string backup = await CreateAsync("auto", cancellationToken);
        DeleteOldAutomaticBackups();
        return backup;
    }

    public async Task<bool> HasPendingSchemaChangesAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.DatabaseFilePath))
        {
            return false;
        }

        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        IEnumerable<string> pending = await context.Database.GetPendingMigrationsAsync(cancellationToken);
        IEnumerable<string> applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
        return applied.Any() && pending.Any();
    }

    public async Task RestoreAsync(string backupFilePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFilePath);
        string sourcePath = Path.GetFullPath(backupFilePath);
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("La copia seleccionada no existe.", sourcePath);
        }

        await ValidateCompatibleAsync(sourcePath, cancellationToken);
        await CreateBeforeRestoreAsync(cancellationToken);

        string temporaryPath = Path.Combine(paths.DataDirectory, $"restore-{Guid.NewGuid():N}.tmp");
        string rollbackPath = Path.Combine(paths.DataDirectory, $"rollback-{Guid.NewGuid():N}.tmp");
        try
        {
            await CopyFileAsync(sourcePath, temporaryPath, cancellationToken);
            await ValidateCompatibleAsync(temporaryPath, cancellationToken);
            SqliteConnection.ClearAllPools();

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
    }

    private async Task<string> CreateAsync(string prefix, CancellationToken cancellationToken)
    {
        paths.EnsureDirectories();
        if (!File.Exists(paths.DatabaseFilePath))
        {
            throw new InvalidOperationException("Todavía no existe una base de datos para copiar.");
        }

        string destination = Path.Combine(
            paths.BackupsDirectory,
            $"{prefix}-{timeProvider.GetUtcNow():yyyyMMdd-HHmmssfff}.db");
        await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            SqliteConnection.ClearAllPools();
            using var source = new SqliteConnection(DatabaseConfiguration.CreateConnectionString(paths.DatabaseFilePath));
            using var target = new SqliteConnection($"Data Source={destination};Mode=ReadWriteCreate;Pooling=False");
            source.Open();
            target.Open();
            source.BackupDatabase(target);
        }, cancellationToken);
        await ValidateCompatibleAsync(destination, cancellationToken);
        return destination;
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
}
