using System.Reflection;
using PeluqueriaAdmin.Application.Updates;
using Velopack;
using Velopack.Sources;

namespace PeluqueriaAdmin.App.Updates;

public sealed class VelopackUpdateService : IUpdateService
{
    private const string RepositoryUrl = "https://github.com/colombianitov2/peluqueria-admin";
    private readonly UpdateManager manager;

    public VelopackUpdateService()
    {
        string informationalVersion = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? string.Empty;
        bool includePrereleases = informationalVersion.Contains('-', StringComparison.Ordinal);
        manager = new UpdateManager(
            new GithubSource(RepositoryUrl, accessToken: null, prerelease: includePrereleases));
    }

    public bool CanApplyUpdate => manager.IsInstalled && manager.UpdatePendingRestart is not null;

    public async Task<UpdateCheckResult> CheckAndDownloadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!manager.IsInstalled)
        {
            return new UpdateCheckResult(UpdateCheckStatus.NotInstalled, null);
        }

        if (manager.UpdatePendingRestart is { } pending)
        {
            return new UpdateCheckResult(UpdateCheckStatus.ReadyToInstall, pending.Version.ToString());
        }

        UpdateInfo? update = await manager.CheckForUpdatesAsync();
        if (update is null)
        {
            return new UpdateCheckResult(UpdateCheckStatus.UpToDate, manager.CurrentVersion?.ToString());
        }

        await manager.DownloadUpdatesAsync(update, cancelToken: cancellationToken);
        return new UpdateCheckResult(
            UpdateCheckStatus.ReadyToInstall,
            update.TargetFullRelease.Version.ToString());
    }

    public void ApplyAndRestart()
    {
        if (!CanApplyUpdate)
        {
            throw new InvalidOperationException("No hay una actualización descargada para instalar.");
        }

        manager.ApplyUpdatesAndRestart(manager.UpdatePendingRestart);
    }
}
