using System.Collections.ObjectModel;
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
    private bool navigationInitialized;

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
        NavigationItems =
        [
            new("Inicio", false),
            new(AdministrationViewModel.LocalUseModule, true),
            new(AdministrationViewModel.CollaboratorsModule, true),
            new(AdministrationViewModel.SalesModule, true),
            new(AdministrationViewModel.InventoryModule, true),
            new(AdministrationViewModel.OtherIncomeModule, true),
            new(AdministrationViewModel.ExpensesModule, true),
            new(AdministrationViewModel.UnexpectedModule, true),
            new(AdministrationViewModel.ObligationsModule, true),
            new(AdministrationViewModel.MaintenanceModule, true),
            new(AdministrationViewModel.PayrollModule, true),
            new(AdministrationViewModel.MonthlySummaryModule, true),
            new(AdministrationViewModel.AnnualBalanceModule, true),
            new(AdministrationViewModel.CashFlowModule, true),
            new("Ajustes", false),
        ];
        currentPage = this;
        selectedNavigationItem = NavigationItems[0];
        navigationInitialized = true;
    }

    public SettingsViewModel Settings { get; }

    public AdministrationViewModel Administration { get; }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    [ObservableProperty]
    private object? currentPage;

    [ObservableProperty]
    private NavigationItem? selectedNavigationItem;

    [ObservableProperty]
    private Task currentNavigationTask = Task.CompletedTask;

    [ObservableProperty]
    private string fechaActual = DateTime.Today.ToString("D", CultureInfo.GetCultureInfo("es-ES"));

    [ObservableProperty]
    private string estadoServiciosEImpuestos = "Sin obligaciones pendientes";

    [ObservableProperty]
    private string estadoPersonasConPagosPendientes = "Sin personas con deuda";

    [ObservableProperty]
    private string estadoPuntoDeEquilibrio = "Sin faltante calculado";

    [RelayCommand]
    private Task ShowHomeAsync() => NavigateToAsync("Inicio");

    [RelayCommand]
    private Task ShowSettingsAsync() => NavigateToAsync("Ajustes");

    [RelayCommand]
    private Task ShowModuleAsync(string module) => NavigateToAsync(module);

    public async Task NavigateToAsync(string name)
    {
        NavigationItem item = NavigationItems.SingleOrDefault(candidate => candidate.Name == name)
            ?? throw new ArgumentException("El módulo solicitado no existe.", nameof(name));
        if (!ReferenceEquals(SelectedNavigationItem, item))
        {
            SelectedNavigationItem = item;
            await CurrentNavigationTask;
            return;
        }

        CurrentNavigationTask = NavigateCoreAsync(item);
        await CurrentNavigationTask;
    }

    partial void OnSelectedNavigationItemChanged(NavigationItem? value)
    {
        if (navigationInitialized && value is not null)
        {
            CurrentNavigationTask = NavigateCoreAsync(value);
        }
    }

    private async Task NavigateCoreAsync(NavigationItem item)
    {
        if (item.Name == "Inicio")
        {
            CurrentPage = this;
            await RefreshHomeAsync();
            return;
        }

        if (item.Name == "Ajustes")
        {
            CurrentPage = Settings;
            return;
        }

        await Administration.SelectModuleAsync(item.Name);
        CurrentPage = Administration;
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

    public async Task FlushPendingAsync()
    {
        await Settings.FlushPendingAsync();
        await Administration.FlushPendingAsync();
    }
}
