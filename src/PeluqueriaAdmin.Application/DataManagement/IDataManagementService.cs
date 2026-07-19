namespace PeluqueriaAdmin.Application.DataManagement;

public interface IDataManagementService
{
    string BackupsDirectory { get; }

    string ExportsDirectory { get; }

    Task<string> CreateManualBackupAsync(CancellationToken cancellationToken = default);

    Task RestoreAsync(string backupFilePath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ExportAsync(CancellationToken cancellationToken = default);
}
