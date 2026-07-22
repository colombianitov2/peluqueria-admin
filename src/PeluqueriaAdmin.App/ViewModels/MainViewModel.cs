using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
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

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public ObservableCollection<MaintenanceNotificationRow> MaintenanceNotifications { get; } = [];

    public ObservableCollection<ObligationNotificationRow> ObligationNotifications { get; } = [];

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
    [ObservableProperty] private int obligationNotificationCount;
    [ObservableProperty] private bool isMaintenanceNotificationsOpen;
    [ObservableProperty] private bool isObligationNotificationsOpen;

    [RelayCommand]
    private async Task GoToMaintenanceAsync()
    {
        IsMaintenanceNotificationsOpen = false;
        await NavigateToAsync(AdministrationViewModel.MaintenanceModule);
    }

    [RelayCommand]
    private async Task GoToObligationsAsync()
    {
        IsObligationNotificationsOpen = false;
        await NavigateToAsync(AdministrationViewModel.ObligationsModule);
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

        ObligationNotifications.Clear();
        foreach (Obligation obligation in data.Obligations
            .Where(item => item.DueDate <= today
                && item.Status(data.ObligationPayments.Where(payment => payment.ObligationId == item.Id), today)
                    != ObligationStatus.Paid)
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.Name))
        {
            ObligationNotifications.Add(new ObligationNotificationRow(
                obligation.Name,
                ObligationTypeName(obligation.Type),
                obligation.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                $"{ApplicationCurrency.Code} {obligation.ExpectedAmount.ToDecimal():N2}",
                obligation.DueDate == today ? "Hoy" : "Vencida"));
        }
        ObligationNotificationCount = ObligationNotifications.Count;

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
    }

    private static string ObligationTypeName(ObligationType type) => type switch
    {
        ObligationType.Service => "Servicio",
        ObligationType.Tax => "Impuesto",
        ObligationType.OtherRecurring => "Otra obligación",
        _ => type.ToString(),
    };

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
        if (!ReferenceEquals(CurrentPage, this)) return;
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            _ = dispatcher.InvokeAsync(RefreshHomeAsync);
        }
        else
        {
            _ = RefreshHomeAsync();
        }
    }
}

public sealed record MaintenanceNotificationRow(string Asset, string Type, string Date, string Status);

public sealed record ObligationNotificationRow(string Name, string Type, string Date, string Amount, string Status);
