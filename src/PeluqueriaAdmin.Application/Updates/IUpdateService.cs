namespace PeluqueriaAdmin.Application.Updates;

public enum UpdateCheckStatus
{
    NotInstalled,
    UpToDate,
    ReadyToInstall,
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, string? Version);

public interface IUpdateService
{
    bool CanApplyUpdate { get; }

    Task<UpdateCheckResult> CheckAndDownloadAsync(CancellationToken cancellationToken = default);

    void ApplyAndRestart();
}
