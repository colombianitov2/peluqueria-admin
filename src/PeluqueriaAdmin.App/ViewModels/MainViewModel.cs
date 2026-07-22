using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AdministrationService administrationService;
    private readonly GetSettingsUseCase getSettings;
    private readonly TimeProvider timeProvider;
    private ITimer? dayChangeTimer;
    private bool navigationInitialized;

    public MainViewModel(
        SettingsViewModel settings,
        AdministrationViewModel administration,
        LocalUseViewModel localUse,
        CollaboratorsViewModel collaborators,
        SalesViewModel sales,
        InventoryViewModel inventory,
        MaintenanceViewModel maintenance,
        ObligationsViewModel obligations,
        NotesViewModel notes,
        AdministrationService administrationService,
        GetSettingsUseCase getSettings,
        TimeProvider timeProvider)
    {
        Settings = settings;
        Administration = administration;
        LocalUse = localUse;
        Collaborators = collaborators;
        Sales = sales;
        Inventory = inventory;
        Maintenance = maintenance;
        Obligations = obligations;
        Notes = notes;
        this.administrationService = administrationService;
        this.getSettings = getSettings;
        this.timeProvider = timeProvider;
        administrationService.DataChanged += OnAdministrationDataChanged;
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
            new(AdministrationViewModel.MonthlySummaryModule, true),
            new(AdministrationViewModel.AnnualBalanceModule, true),
            new("Ajustes", false),
            new("Notas", false),
        ];
        currentPage = this;
        selectedNavigationItem = NavigationItems[0];
        navigationInitialized = true;
        ScheduleDayChangeRefresh();
    }

    public SettingsViewModel Settings { get; }

    public AdministrationViewModel Administration { get; }

    public LocalUseViewModel LocalUse { get; }

    public CollaboratorsViewModel Collaborators { get; }

    public SalesViewModel Sales { get; }

    public InventoryViewModel Inventory { get; }

    public MaintenanceViewModel Maintenance { get; }

    public NotesViewModel Notes { get; }

    public ObligationsViewModel Obligations { get; }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public ObservableCollection<MaintenanceNotificationRow> MaintenanceNotifications { get; } = [];

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

    [ObservableProperty]
    private string precioSugeridoPorSilla = "No se puede calcular: no hay sillas ocupadas";

    [ObservableProperty] private int maintenanceNotificationCount;
    [ObservableProperty] private bool isMaintenanceNotificationsOpen;

    [RelayCommand]
    private async Task GoToMaintenanceAsync()
    {
        IsMaintenanceNotificationsOpen = false;
        await NavigateToAsync(AdministrationViewModel.MaintenanceModule);
    }

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

        if (item.Name == "Notas")
        {
            await Notes.LoadAsync();
            CurrentPage = Notes;
            return;
        }

        if (item.Name == AdministrationViewModel.LocalUseModule)
        {
            await LocalUse.LoadAsync();
            CurrentPage = LocalUse;
            return;
        }

        if (item.Name == AdministrationViewModel.CollaboratorsModule)
        {
            await Collaborators.LoadAsync();
            CurrentPage = Collaborators;
            return;
        }

        if (item.Name == AdministrationViewModel.SalesModule)
        {
            await Sales.LoadAsync();
            CurrentPage = Sales;
            return;
        }

        if (item.Name == AdministrationViewModel.InventoryModule)
        {
            await Inventory.LoadAsync();
            CurrentPage = Inventory;
            return;
        }

        if (item.Name == AdministrationViewModel.MaintenanceModule)
        {
            await Maintenance.LoadAsync();
            CurrentPage = Maintenance;
            return;
        }

        if (item.Name == AdministrationViewModel.ObligationsModule)
        {
            await Obligations.LoadAsync();
            CurrentPage = Obligations;
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
        AdministrationData data = await administrationService.GenerateScheduledRecordsAsync(today);
        SettingsDto settings = await getSettings.ExecuteAsync();
        FechaActual = localNow.ToString("D", CultureInfo.GetCultureInfo("es-ES"));

        MaintenanceNotifications.Clear();
        foreach (MaintenanceRecord maintenance in data.MaintenanceRecords
            .Where(item => item.NeedsAttention(today))
            .OrderBy(item => item.ScheduledDate)
            .ThenBy(item => item.Asset))
        {
            MaintenanceNotifications.Add(new MaintenanceNotificationRow(
                maintenance.Asset,
                maintenance.MaintenanceType,
                maintenance.ScheduledDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                maintenance.ScheduledDate == today ? "Hoy" : "Vencido"));
        }
        MaintenanceNotificationCount = MaintenanceNotifications.Count;

        HomeDashboard dashboard = HomeDashboardCalculator.Calculate(
            data,
            Percentage.FromPercent(settings.CollaboratorProfitPercent),
            today);
        string[] pendingObligations = dashboard.Obligations
            .Select(item => $"{item.DueDate:yyyy-MM-dd} · {item.Name}")
            .ToArray();
        EstadoServiciosEImpuestos = pendingObligations.Length == 0
            ? "Sin obligaciones pendientes"
            : string.Join(Environment.NewLine, pendingObligations);

        string[] debts = dashboard.Debts
            .Select(item => $"{item.Name} · {ApplicationCurrency.Code} {item.Amount.ToDecimal():N2}")
            .ToArray();
        EstadoPersonasConPagosPendientes = debts.Length == 0
            ? "Sin personas con deuda"
            : string.Join(Environment.NewLine, debts);

        EstadoPuntoDeEquilibrio = $"{ApplicationCurrency.Code} {dashboard.MissingMinorUnits / 100m:N2}";

        SuggestedChairPrice suggested = SuggestedChairPriceCalculator.Calculate(
            data,
            Money.FromDecimal(settings.WeeklyUsageFee),
            month,
            today);
        PrecioSugeridoPorSilla = suggested.CanCalculate
            ? $"Precio semanal actual: {ApplicationCurrency.Code} {suggested.CurrentWeeklyMinorUnits / 100m:N2}{Environment.NewLine}"
                + $"Precio semanal sugerido por silla ocupada: {ApplicationCurrency.Code} {suggested.SuggestedWeeklyPerChairMinorUnits / 100m:N2}{Environment.NewLine}"
                + $"Equivalente mensual sugerido: {ApplicationCurrency.Code} {suggested.SuggestedMonthlyPerChairMinorUnits / 100m:N2}{Environment.NewLine}"
                + suggested.Explanation
            : suggested.Explanation;
    }

    public async Task FlushPendingAsync()
    {
        await Settings.FlushPendingAsync();
        await Administration.FlushPendingAsync();
        await LocalUse.FlushPendingAsync();
        await Collaborators.FlushPendingAsync();
        await Sales.FlushPendingAsync();
        await Inventory.FlushPendingAsync();
        await Maintenance.FlushPendingAsync();
        await Notes.FlushPendingAsync();
        await Obligations.FlushPendingAsync();
    }

    private void OnAdministrationDataChanged(object? sender, EventArgs eventArgs)
    {
        if (ReferenceEquals(CurrentPage, this))
        {
            _ = RefreshHomeAsync();
        }
    }

    private void ScheduleDayChangeRefresh()
    {
        DateTimeOffset localNow = timeProvider.GetLocalNow();
        DateTimeOffset nextDay = new DateTimeOffset(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            0,
            0,
            0,
            localNow.Offset).AddDays(1);
        TimeSpan dueTime = nextDay - localNow;
        if (dayChangeTimer is null)
        {
            dayChangeTimer = timeProvider.CreateTimer(OnDayChanged, null, dueTime, Timeout.InfiniteTimeSpan);
        }
        else
        {
            dayChangeTimer.Change(dueTime, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDayChanged(object? state)
    {
        ScheduleDayChangeRefresh();
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            _ = dispatcher.InvokeAsync(RefreshAfterDayChangeAsync);
        }
        else
        {
            _ = RefreshAfterDayChangeAsync();
        }
    }

    private async Task RefreshAfterDayChangeAsync()
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        await administrationService.GenerateScheduledRecordsAsync(today);
        if (ReferenceEquals(CurrentPage, this)) await RefreshHomeAsync();
        else if (ReferenceEquals(CurrentPage, LocalUse)) await LocalUse.LoadAsync();
        else if (ReferenceEquals(CurrentPage, Maintenance)) await Maintenance.LoadAsync();
    }
}

public sealed record MaintenanceNotificationRow(string Asset, string Type, string Date, string Status);
