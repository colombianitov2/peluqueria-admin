using System.IO;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PeluqueriaAdmin.App.Updates;
using PeluqueriaAdmin.App.ViewModels;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.DataManagement;
using PeluqueriaAdmin.Application.Drafts;
using PeluqueriaAdmin.Application.Exporting;
using PeluqueriaAdmin.Application.Notes;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Application.Updates;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Infrastructure.Administration;
using PeluqueriaAdmin.Infrastructure.Drafts;
using PeluqueriaAdmin.Infrastructure.Exporting;
using PeluqueriaAdmin.Infrastructure.Notes;
using PeluqueriaAdmin.Infrastructure.Persistence;
using PeluqueriaAdmin.Infrastructure.Settings;
using PeluqueriaAdmin.Infrastructure.Storage;
using Velopack;

namespace PeluqueriaAdmin.App;

public partial class App : System.Windows.Application
{
    private const string SingleInstanceMutexName = @"Local\Colombianito.PeluqueriaAdmin";
    private static Mutex? singleInstanceMutex;
    private ServiceProvider? serviceProvider;
    private DatabaseBackupService? backupService;
    private CancellationTokenSource? backupLoopCancellation;
    private Task? backupLoopTask;
    private ApplicationExitCoordinator? exitCoordinator;

    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().SetArgs(args).SetAutoApplyOnStartup(false).Run();

        singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "Peluquería Admin ya está abierta. Usa la ventana existente.",
                "Peluquería Admin",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            singleInstanceMutex.Dispose();
            singleInstanceMutex = null;
            return;
        }

        try
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        finally
        {
            singleInstanceMutex.ReleaseMutex();
            singleInstanceMutex.Dispose();
            singleInstanceMutex = null;
        }
    }

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            serviceProvider = ConfigureServices();
            backupService = serviceProvider.GetRequiredService<DatabaseBackupService>();
            await serviceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
            DateOnly today = DateOnly.FromDateTime(DateTime.Today);
            await serviceProvider.GetRequiredService<AdministrationService>()
                .GenerateScheduledRecordsAsync(YearMonth.From(today).LastDay);

            SettingsViewModel settingsViewModel = serviceProvider.GetRequiredService<SettingsViewModel>();
            await settingsViewModel.LoadAsync();
            await serviceProvider.GetRequiredService<MainViewModel>().RefreshHomeAsync();

            MainWindow window = serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
            backupLoopCancellation = new CancellationTokenSource();
            backupLoopTask = RunAutomaticBackupLoopAsync(
                backupService,
                backupLoopCancellation.Token);
            _ = settingsViewModel.CheckForUpdatesOnStartupAsync();
        }
        catch (Exception exception)
        {
            string? logPath = TryWriteStartupFailureLog(exception);
            string rootCause = exception.GetBaseException().Message;
            string message =
                "No fue posible preparar los datos del programa. "
                + "La aplicación se cerrará sin abrir la ventana principal."
                + $"\n\nCausa raíz: {rootCause}";

            if (!string.IsNullOrWhiteSpace(logPath))
            {
                message += $"\n\nDiagnóstico guardado en:\n{logPath}";
            }

#if DEBUG
            message += $"\n\nDetalle de desarrollo:\n{exception}";
#endif
            System.Windows.MessageBox.Show(
                message,
                "Error al iniciar Peluquería Admin",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        exitCoordinator ??= new ApplicationExitCoordinator(
            () => backupLoopCancellation?.Cancel(),
            CreateExitBackupAsync,
            ReportExitBackupFailure,
            () =>
            {
                backupLoopCancellation?.Dispose();
                backupLoopCancellation = null;
                backupLoopTask = null;
                serviceProvider?.Dispose();
            });
        exitCoordinator.RunOnce(() => base.OnExit(e));
    }

    private async Task CreateExitBackupAsync()
    {
        if (backupService is not null)
        {
            await backupService.CreateAutomaticIfNeededAsync();
        }
    }

    private static void ReportExitBackupFailure(Exception exception)
    {
        try
        {
            string? testDataRoot =
                Environment.GetEnvironmentVariable("PELUQUERIA_ADMIN_DATA_ROOT");
            ApplicationPaths paths = string.IsNullOrWhiteSpace(testDataRoot)
                ? ApplicationPaths.ForCurrentUser()
                : ApplicationPaths.FromRoot(testDataRoot);
            paths.EnsureDirectories();

            string logPath = Path.Combine(
                paths.LogsDirectory,
                $"shutdown-error-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
            string diagnostic =
                $"Fecha local: {DateTime.Now:O}{Environment.NewLine}"
                + $"Fecha UTC: {DateTime.UtcNow:O}{Environment.NewLine}"
                + $"Proceso: {Environment.ProcessPath}{Environment.NewLine}"
                + $"Raíz de datos: {paths.RootDirectory}{Environment.NewLine}"
                + Environment.NewLine
                + exception;

            File.WriteAllText(logPath, diagnostic);
        }
        catch
        {
            // Un fallo del diagnóstico no debe impedir la salida de la aplicación.
        }
    }

    private static async Task RunAutomaticBackupLoopAsync(
        DatabaseBackupService backupService,
        CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(30), cancellationToken);
                try
                {
                    await backupService.CreateAutomaticIfNeededAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch
                {
                    // La siguiente comprobación vuelve a intentarlo sin afectar el uso del programa.
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static string? TryWriteStartupFailureLog(Exception exception)
    {
        try
        {
            string? testDataRoot =
                Environment.GetEnvironmentVariable("PELUQUERIA_ADMIN_DATA_ROOT");
            ApplicationPaths paths = string.IsNullOrWhiteSpace(testDataRoot)
                ? ApplicationPaths.ForCurrentUser()
                : ApplicationPaths.FromRoot(testDataRoot);
            paths.EnsureDirectories();

            string logPath = Path.Combine(
                paths.LogsDirectory,
                $"startup-error-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");

            string diagnostic =
                $"Fecha local: {DateTime.Now:O}{Environment.NewLine}"
                + $"Fecha UTC: {DateTime.UtcNow:O}{Environment.NewLine}"
                + $"Versión del ensamblado: "
                + $"{typeof(App).Assembly.GetName().Version}{Environment.NewLine}"
                + $"Sistema: {Environment.OSVersion}{Environment.NewLine}"
                + $"Proceso: {Environment.ProcessPath}{Environment.NewLine}"
                + $"Raíz de datos: {paths.RootDirectory}{Environment.NewLine}"
                + $"Base de datos: {paths.DatabaseFilePath}{Environment.NewLine}"
                + Environment.NewLine
                + "Excepción raíz:"
                + Environment.NewLine
                + exception.GetBaseException()
                + Environment.NewLine
                + Environment.NewLine
                + "Excepción completa:"
                + Environment.NewLine
                + exception;

            File.WriteAllText(logPath, diagnostic);
            return logPath;
        }
        catch
        {
            return null;
        }
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        string? testDataRoot = Environment.GetEnvironmentVariable("PELUQUERIA_ADMIN_DATA_ROOT");
        ApplicationPaths paths = string.IsNullOrWhiteSpace(testDataRoot)
            ? ApplicationPaths.ForCurrentUser()
            : ApplicationPaths.FromRoot(testDataRoot);

        services.AddSingleton(paths);
        services.AddSingleton(TimeProvider.System);
        services.AddDbContextFactory<PeluqueriaDbContext>(options =>
            DatabaseConfiguration.Configure(options, paths.DatabaseFilePath));
        services.AddSingleton<ISettingsRepository, EfSettingsRepository>();
        services.AddSingleton<IAdministrationRepository, EfAdministrationRepository>();
        services.AddSingleton<IFormDraftStore, EfFormDraftStore>();
        services.AddSingleton<INoteRepository, EfNoteRepository>();
        services.AddSingleton<IUserDesktopPath, CurrentUserDesktopPath>();
        services.AddSingleton<IExcelWorkbookWriter, ClosedXmlWorkbookWriter>();
        services.AddSingleton<IExcelExportService, ExcelExportService>();
        services.AddSingleton<DatabaseBackupService>();
        services.AddSingleton<IDataManagementService, BackupDataManagementService>();
        services.AddSingleton<IUpdateService, VelopackUpdateService>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<AdministrationService>();
        services.AddSingleton<GetSettingsUseCase>();
        services.AddSingleton<SaveSettingsUseCase>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<AdministrationViewModel>();
        services.AddSingleton<LocalUseViewModel>();
        services.AddSingleton<CollaboratorsViewModel>();
        services.AddSingleton<SalesViewModel>();
        services.AddSingleton<InventoryViewModel>();
        services.AddSingleton<MaintenanceViewModel>();
        services.AddSingleton<ObligationsViewModel>();
        services.AddSingleton<NotesViewModel>();
        services.AddSingleton<ManualViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}

internal sealed class ApplicationExitCoordinator(
    Action prepareExit,
    Func<Task> createBackupAsync,
    Action<Exception> reportFailure,
    Action cleanup)
{
    private int hasRun;

    public void RunOnce(Action baseExit)
    {
        if (Interlocked.Exchange(ref hasRun, 1) != 0)
        {
            return;
        }

        try
        {
            try
            {
                prepareExit();
                ExitBackupRunner.Run(createBackupAsync, reportFailure);
            }
            catch (Exception exception)
            {
                ReportFailureSafely(reportFailure, exception);
            }
        }
        finally
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                ReportFailureSafely(reportFailure, exception);
            }
            finally
            {
                baseExit();
            }
        }
    }

    private static void ReportFailureSafely(
        Action<Exception> reportFailure,
        Exception exception)
    {
        try
        {
            reportFailure(exception);
        }
        catch
        {
            // El diagnóstico no debe impedir la salida de la aplicación.
        }
    }
}

internal static class ExitBackupRunner
{
    public static void Run(
        Func<Task> createBackupAsync,
        Action<Exception> reportFailure)
    {
        try
        {
            Task.Run(async () =>
                    await createBackupAsync().ConfigureAwait(false))
                .GetAwaiter()
                .GetResult();
        }
        catch (Exception exception)
        {
            try
            {
                reportFailure(exception);
            }
            catch
            {
                // El diagnóstico no debe impedir la salida de la aplicación.
            }
        }
    }
}
