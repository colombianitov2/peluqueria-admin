using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using PeluqueriaAdmin.App.ViewModels;
using PeluqueriaAdmin.Infrastructure.Storage;

[assembly: InternalsVisibleTo("PeluqueriaAdmin.App.Tests")]

namespace PeluqueriaAdmin.App;

public partial class MainWindow : Window
{
    private readonly WindowCloseCoordinator closeCoordinator;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        closeCoordinator = new WindowCloseCoordinator(
            viewModel.FlushPendingAsync,
            action => _ = Dispatcher.BeginInvoke(
                DispatcherPriority.ApplicationIdle,
                action),
            () =>
            {
                if (IsLoaded)
                {
                    Close();
                }
            },
            ReportClosingFailure);
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        await closeCoordinator.HandleClosingAsync(e);
    }

    private static void ReportClosingFailure(Exception exception)
    {
        string? logPath = TryWriteClosingFailureLog(exception);
        string message =
            "No fue posible completar el guardado antes de cerrar. "
            + "La aplicación permanecerá abierta para proteger los datos.";

        if (!string.IsNullOrWhiteSpace(logPath))
        {
            message += $"\n\nDiagnóstico guardado en:\n{logPath}";
        }

        MessageBox.Show(
            message,
            "No se pudo cerrar Peluquería Admin",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static string? TryWriteClosingFailureLog(Exception exception)
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
                $"closing-error-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
            string diagnostic =
                $"Fecha local: {DateTime.Now:O}{Environment.NewLine}"
                + $"Fecha UTC: {DateTime.UtcNow:O}{Environment.NewLine}"
                + $"Proceso: {Environment.ProcessPath}{Environment.NewLine}"
                + $"Raíz de datos: {paths.RootDirectory}{Environment.NewLine}"
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
}

internal sealed class WindowCloseCoordinator(
    Func<Task> flushPendingAsync,
    Action<Action> scheduleClose,
    Action close,
    Action<Exception> reportFailure)
{
    private bool flushInProgress;
    private bool closeAuthorized;
    private bool closeScheduled;

    public async Task HandleClosingAsync(CancelEventArgs eventArgs)
    {
        if (closeAuthorized)
        {
            return;
        }

        eventArgs.Cancel = true;
        if (flushInProgress)
        {
            return;
        }

        flushInProgress = true;
        try
        {
            await flushPendingAsync();
            closeAuthorized = true;
            if (!closeScheduled)
            {
                closeScheduled = true;
                scheduleClose(CompleteClose);
            }
        }
        catch (Exception exception)
        {
            closeAuthorized = false;
            closeScheduled = false;
            ReportFailureSafely(exception);
        }
        finally
        {
            flushInProgress = false;
        }
    }

    private void CompleteClose()
    {
        try
        {
            close();
        }
        catch (Exception exception)
        {
            closeAuthorized = false;
            closeScheduled = false;
            ReportFailureSafely(exception);
        }
    }

    private void ReportFailureSafely(Exception exception)
    {
        try
        {
            reportFailure(exception);
        }
        catch
        {
            // El diagnóstico no debe convertir un fallo de guardado en una excepción no observada.
        }
    }
}
