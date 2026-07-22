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
    private ServiceProvider? serviceProvider;

    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().SetArgs(args).SetAutoApplyOnStartup(false).Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            serviceProvider = ConfigureServices();
            await serviceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
            await serviceProvider.GetRequiredService<AdministrationService>()
                .GenerateScheduledRecordsAsync(DateOnly.FromDateTime(DateTime.Today));

            SettingsViewModel settingsViewModel = serviceProvider.GetRequiredService<SettingsViewModel>();
            await settingsViewModel.LoadAsync();
            await serviceProvider.GetRequiredService<MainViewModel>().RefreshHomeAsync();

            MainWindow window = serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
            _ = settingsViewModel.CheckForUpdatesOnStartupAsync();
        }
        catch (Exception exception)
        {
            string message = "No fue posible preparar los datos del programa. La aplicación se cerrará sin abrir la ventana principal.";
#if DEBUG
            message += $"\n\nDetalle de desarrollo:\n{exception}";
#else
            message += $"\n\nCausa: {exception.Message}";
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
        serviceProvider?.Dispose();
        base.OnExit(e);
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
        services.AddSingleton<IDataManagementService, CsvDataManagementService>();
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
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
