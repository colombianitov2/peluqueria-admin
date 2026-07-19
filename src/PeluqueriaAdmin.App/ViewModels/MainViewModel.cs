using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AdministrationService administrationService;
    private readonly GetSettingsUseCase getSettings;
    private readonly TimeProvider timeProvider;

    public MainViewModel(
        SettingsViewModel settings,
        AdministrationViewModel administration,
        AdministrationService administrationService,
        GetSettingsUseCase getSettings,
        TimeProvider timeProvider)
    {
        Settings = settings;
        Administration = administration;
        this.administrationService = administrationService;
        this.getSettings = getSettings;
        this.timeProvider = timeProvider;
    }

    public SettingsViewModel Settings { get; }

    public AdministrationViewModel Administration { get; }

    [ObservableProperty]
    private string fechaActual = DateTime.Today.ToString("D", CultureInfo.GetCultureInfo("es-ES"));

    [ObservableProperty]
    private string estadoServiciosEImpuestos = "Sin obligaciones pendientes";

    [ObservableProperty]
    private string estadoPersonasConPagosPendientes = "Sin personas con deuda";

    [ObservableProperty]
    private string estadoPuntoDeEquilibrio = "Sin faltante calculado";

    [ObservableProperty]
    private bool isHomeVisible = true;

    [ObservableProperty]
    private bool isSettingsVisible;

    [ObservableProperty]
    private bool isAdministrationVisible;

    [RelayCommand]
    private async Task ShowHomeAsync()
    {
        IsHomeVisible = true;
        IsSettingsVisible = false;
        IsAdministrationVisible = false;
        await RefreshHomeAsync();
    }

    [RelayCommand]
    private void ShowSettings()
    {
        IsHomeVisible = false;
        IsSettingsVisible = true;
        IsAdministrationVisible = false;
    }

    [RelayCommand]
    private async Task ShowModuleAsync(string module)
    {
        IsHomeVisible = false;
        IsSettingsVisible = false;
        IsAdministrationVisible = true;
        await Administration.SelectModuleAsync(module);
    }

    public async Task RefreshHomeAsync()
    {
        DateTime localNow = timeProvider.GetLocalNow().DateTime;
        DateOnly today = DateOnly.FromDateTime(localNow);
        YearMonth month = YearMonth.From(today);
        AdministrationData data = await administrationService.GenerateScheduledRecordsAsync(month.LastDay);
        SettingsDto settings = await getSettings.ExecuteAsync();
        FechaActual = localNow.ToString("D", CultureInfo.GetCultureInfo("es-ES"));

        HomeDashboard dashboard = HomeDashboardCalculator.Calculate(
            data,
            Money.FromDecimal(settings.OptionalSuppliesMonthlyBudget),
            Percentage.FromPercent(settings.CollaboratorProfitPercent),
            today);
        string[] pendingObligations = dashboard.Obligations
            .Select(item => $"{item.DueDate:yyyy-MM-dd} · {item.Name}")
            .ToArray();
        EstadoServiciosEImpuestos = pendingObligations.Length == 0
            ? "Sin obligaciones pendientes"
            : string.Join(Environment.NewLine, pendingObligations);

        string[] debts = dashboard.Debts
            .Select(item => $"{item.Name} · {settings.CurrencyCode} {item.Amount.ToDecimal():N2}")
            .ToArray();
        EstadoPersonasConPagosPendientes = debts.Length == 0
            ? "Sin personas con deuda"
            : string.Join(Environment.NewLine, debts);

        EstadoPuntoDeEquilibrio = $"{settings.CurrencyCode} {dashboard.MissingMinorUnits / 100m:N2}";
    }
}
