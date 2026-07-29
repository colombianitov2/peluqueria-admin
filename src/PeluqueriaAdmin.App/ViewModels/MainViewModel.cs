using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AdministrationService administrationService;
    private readonly GetSettingsUseCase getSettings;
    private readonly TimeProvider timeProvider;
    private ITimer? dayChangeTimer;
    private bool navigationInitialized;
    private bool isRefreshingAfterDataChange;

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
        ManualViewModel manual,
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
        Manual = manual;
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
            new("Manual", false),
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

    public ManualViewModel Manual { get; }

    public ObligationsViewModel Obligations { get; }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public ObservableCollection<MaintenanceNotificationRow> MaintenanceNotifications { get; } = [];

    public ObservableCollection<DailyMovementRow> DailyMovements { get; } = [];

    public ObservableCollection<PendingPaymentRow> PendingPayments { get; } = [];

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
    private string saldoDisponibleDelLocal = "Sin calcular";

    [ObservableProperty]
    private string precioSugeridoPorSilla = "No se puede calcular: no hay sillas ocupadas";

    [ObservableProperty] private int maintenanceNotificationCount;
    [ObservableProperty] private bool isMaintenanceNotificationsOpen;
    [ObservableProperty] private DateTime? movementQueryDate;
    [ObservableProperty] private string dailyMovementsStatus = "Sin configurar: selecciona una fecha.";

    [RelayCommand]
    private async Task ConsultDailyMovementsAsync()
    {
        if (!MovementQueryDate.HasValue)
        {
            DailyMovements.Clear();
            DailyMovementsStatus = "Sin configurar: selecciona una fecha.";
            return;
        }
        AdministrationData data = await administrationService.LoadAsync();
        PopulateDailyMovements(data);
    }

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

        if (item.Name == "Manual")
        {
            CurrentPage = Manual;
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
        AdministrationData data = await administrationService.GenerateScheduledRecordsAsync(
            YearMonth.From(today).LastDay);
        SettingsDto settings = await getSettings.ExecuteAsync();
        FechaActual = localNow.ToString("D", CultureInfo.GetCultureInfo("es-ES"));

        MaintenanceNotifications.Clear();
        foreach (MaintenanceRecord maintenance in data.MaintenanceRecords
            .Where(item => !item.CompletedDate.HasValue)
            .OrderBy(item => item.ScheduledDate)
            .ThenBy(item => item.Asset))
        {
            MaintenanceNotifications.Add(new MaintenanceNotificationRow(
                maintenance.Asset,
                maintenance.MaintenanceType,
                maintenance.ScheduledDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                maintenance.EstimatedCost.HasValue
                    ? $"{ApplicationCurrency.Code} {maintenance.EstimatedCost.Value.ToDecimal():N2}"
                    : "Sin costo estimado",
                maintenance.ScheduledDate == today
                    ? "Hoy"
                    : maintenance.ScheduledDate < today ? "Vencido" : "Futuro"));
        }
        MaintenanceNotificationCount = MaintenanceNotifications.Count;

        Percentage? collaboratorPercentage = settings.CollaboratorProfitPercent.HasValue
            ? Percentage.FromPercent(settings.CollaboratorProfitPercent.Value)
            : null;
        HomeDashboard dashboard = HomeDashboardCalculator.Calculate(
            data,
            collaboratorPercentage,
            today);
        PendingPayments.Clear();
        foreach (PendingHomeObligation item in dashboard.Obligations)
        {
            PendingPayments.Add(new PendingPaymentRow(
                item.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.Name,
                item.Type,
                $"{ApplicationCurrency.Code} {item.Amount.ToDecimal():N2}",
                item.Description,
                item.Status));
        }
        EstadoServiciosEImpuestos = PendingPayments.Count == 0
            ? "Sin obligaciones pendientes"
            : $"{PendingPayments.Count} pago(s) pendiente(s)";

        string[] debts = dashboard.Debts
            .Select(item => $"{item.Name} · {ApplicationCurrency.Code} {item.Amount.ToDecimal():N2}")
            .ToArray();
        EstadoPersonasConPagosPendientes = debts.Length == 0
            ? "Sin personas con deuda"
            : string.Join(Environment.NewLine, debts);

        if (collaboratorPercentage.HasValue)
        {
            FinancialMonthSnapshot financial = FinancialMonthCalculator.Calculate(
                data,
                collaboratorPercentage.Value,
                month);
            EstadoPuntoDeEquilibrio =
                $"{ApplicationCurrency.Code} {financial.ShortfallMinorUnits / 100m:N2}";
            MonthlyCashBreakdown cash = AdministrationReports.MonthlyCash(
                data,
                collaboratorPercentage.Value,
                month);
            SaldoDisponibleDelLocal =
                $"{ApplicationCurrency.Code} {cash.CarryOutMinorUnits / 100m:N2}";
        }
        else
        {
            EstadoPuntoDeEquilibrio =
                "Sin calcular: configura Ganancia colaboradores (%) en Ajustes.";
            SaldoDisponibleDelLocal =
                "Sin calcular: configura Ganancia colaboradores (%) en Ajustes.";
        }
        PopulateDailyMovements(data);

        if (settings.DailyUsageFee.HasValue && collaboratorPercentage.HasValue)
        {
            SuggestedChairPrice suggested = SuggestedChairPriceCalculator.Calculate(
                data,
                Money.FromDecimal(settings.DailyUsageFee.Value),
                collaboratorPercentage.Value,
                month,
                today);
            PrecioSugeridoPorSilla = suggested.CanCalculate
                ? $"Tarifa diaria actual: {ApplicationCurrency.Code} {suggested.CurrentDailyMinorUnits / 100m:N2}{Environment.NewLine}"
                    + $"Tarifa diaria sugerida por día-silla: {ApplicationCurrency.Code} {suggested.SuggestedDailyPerChairMinorUnits / 100m:N2}{Environment.NewLine}"
                    + $"Días-silla cobrables del mes: {suggested.ChargeableChairDays}{Environment.NewLine}"
                    + $"Ingreso proyectado por sillas: {ApplicationCurrency.Code} {suggested.ProjectedChairIncomeMinorUnits / 100m:N2}{Environment.NewLine}"
                    + suggested.Explanation
                : suggested.Explanation;
        }
        else
        {
            var missing = new List<string>();
            if (!settings.DailyUsageFee.HasValue)
                missing.Add("Tarifa diaria por uso del local (USD)");
            if (!collaboratorPercentage.HasValue)
                missing.Add("Ganancia colaboradores (%)");
            PrecioSugeridoPorSilla =
                $"Sin calcular: configura {string.Join(" y ", missing)} en Ajustes.";
        }
    }

    private void PopulateDailyMovements(AdministrationData data)
    {
        DailyMovements.Clear();
        if (!MovementQueryDate.HasValue)
        {
            DailyMovementsStatus = "Sin configurar: selecciona una fecha.";
            return;
        }
        DateOnly date = DateOnly.FromDateTime(MovementQueryDate.Value);
        foreach (var item in DailyActivityQuery.ForLocalDate(data.ActivityRecords, date, TimeZoneInfo.Local))
        {
            long? amount = ActivityAmount(data, item.EntityId);
            DateTime localOccurrence = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(item.OccurredUtc, DateTimeKind.Utc),
                TimeZoneInfo.Local);
            DailyMovements.Add(new DailyMovementRow(localOccurrence.ToString("HH:mm:ss", CultureInfo.InvariantCulture),
                item.Module, item.Action, item.Summary, item.Description ?? string.Empty,
                amount.HasValue ? $"{ApplicationCurrency.Code} {amount.Value / 100m:N2}" : string.Empty,
                item.Action == "Eliminación" ? "Eliminado lógicamente" : "Registrado"));
        }
        DailyMovementsStatus = DailyMovements.Count == 0
            ? "Sin movimientos registrados para la fecha consultada."
            : $"{DailyMovements.Count} movimiento(s) encontrado(s).";
    }

    private static long? ActivityAmount(AdministrationData data, Guid? entityId)
    {
        if (!entityId.HasValue) return null;
        Guid id = entityId.Value;
        if (data.LocalUsePayments.SingleOrDefault(x => x.Id == id) is { } local) return local.Amount.MinorUnits;
        if (data.InventoryMovements.SingleOrDefault(x => x.Id == id) is { } inventory) return inventory.CashAmount?.MinorUnits;
        if (data.FinancialEntries.SingleOrDefault(x => x.Id == id) is { } entry) return entry.Amount.MinorUnits;
        if (data.ObligationPayments.SingleOrDefault(x => x.Id == id) is { } obligation) return obligation.Amount.MinorUnits;
        if (data.MaintenanceRecords.SingleOrDefault(x => x.Id == id) is { } maintenance) return maintenance.ActualCost?.MinorUnits ?? maintenance.EstimatedCost?.MinorUnits;
        if (data.DistributionPayments.SingleOrDefault(x => x.Id == id) is { } distribution) return distribution.Amount.MinorUnits;
        if (data.LoanPayments.SingleOrDefault(x => x.Id == id) is { } loanPayment) return loanPayment.Amount.MinorUnits;
        if (data.Loans.SingleOrDefault(x => x.Id == id) is { } loan) return loan.InitialBalance.MinorUnits;
        if (data.FinancialReserves.SingleOrDefault(x => x.Id == id) is { } reserve) return reserve.ReservedAmount.MinorUnits;
        if (data.CollaboratorContributions.SingleOrDefault(x => x.Id == id) is { } contribution)
            return contribution.Amount.MinorUnits;
        if (data.FinancialEvents.Where(x => x.EntityId == id)
            .OrderByDescending(x => x.OccurredUtc).FirstOrDefault() is { } financialEvent)
            return financialEvent.DifferenceMinorUnits;
        return null;
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
        if (isRefreshingAfterDataChange)
        {
            return;
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(RefreshCurrentPageAfterDataChangeAsync);
            return;
        }
        _ = RefreshCurrentPageAfterDataChangeAsync();
    }

    private async Task RefreshCurrentPageAfterDataChangeAsync()
    {
        if (isRefreshingAfterDataChange) return;
        isRefreshingAfterDataChange = true;
        try
        {
            if (ReferenceEquals(CurrentPage, this)) await RefreshHomeAsync();
            else if (ReferenceEquals(CurrentPage, LocalUse)) await LocalUse.RefreshAsync();
            else if (ReferenceEquals(CurrentPage, Collaborators)) await Collaborators.RefreshAsync();
            else if (ReferenceEquals(CurrentPage, Sales)) await Sales.RefreshAsync();
            else if (ReferenceEquals(CurrentPage, Inventory)) await Inventory.RefreshAsync();
            else if (ReferenceEquals(CurrentPage, Maintenance)) await Maintenance.RefreshAsync();
            else if (ReferenceEquals(CurrentPage, Obligations)) await Obligations.RefreshAsync();
            else if (ReferenceEquals(CurrentPage, Settings)) await Settings.LoadAsync();
            else if (ReferenceEquals(CurrentPage, Administration))
                await Administration.SelectModuleAsync(Administration.Title);
        }
        finally
        {
            isRefreshingAfterDataChange = false;
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

public sealed record MaintenanceNotificationRow(string Asset, string Type, string Date, string Cost, string Status);
public sealed record DailyMovementRow(string Time, string Module, string Operation, string Entity,
    string Detail, string Amount, string State);
public sealed record PendingPaymentRow(
    string DueDate,
    string Name,
    string Type,
    string Amount,
    string Description,
    string Status);
