using PeluqueriaAdmin.Application.DataManagement;

namespace PeluqueriaAdmin.Infrastructure.Storage;

public sealed class BackupDataManagementService(
    DatabaseBackupService backupService,
    ApplicationPaths paths) : IDataManagementService
{
    public string BackupsDirectory => paths.BackupsDirectory;

    public string ExportsDirectory => paths.ExportsDirectory;

    public Task<string> CreateManualBackupAsync(CancellationToken cancellationToken = default) =>
        backupService.CreateManualAsync(cancellationToken);

    public Task RestoreAsync(
        string backupFilePath,
        CancellationToken cancellationToken = default) =>
        backupService.RestoreAsync(backupFilePath, cancellationToken);

    public Task<IReadOnlyList<string>> ExportAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "La exportación CSV heredada está deshabilitada. Use la exportación completa a Excel.");
}
