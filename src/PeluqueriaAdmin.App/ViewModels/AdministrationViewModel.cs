using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using PeluqueriaAdmin.Application.Activity;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Drafts;
using PeluqueriaAdmin.Application.Localization;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Drafts;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class AdministrationViewModel(
    AdministrationService service,
    GetSettingsUseCase getSettings,
    IFormDraftStore formDraftStore,
    TimeProvider timeProvider) : ObservableObject
{
    private readonly SemaphoreSlim draftWriteLock = new(1, 1);
    private CancellationTokenSource? editAutosaveCancellation;
    private bool suppressFormTracking;
    private bool isEditing;
    private DateOnly? lastObservedDay;
    public const string LocalUseModule = "Uso del local";
    public const string CollaboratorsModule = "Colaboradores";
    public const string SalesModule = "Ventas";
    public const string InventoryModule = "Inventario";
    public const string OtherIncomeModule = "Otros ingresos";
    public const string ExpensesModule = "Gastos";
    public const string UnexpectedModule = "Imprevistos";
    public const string ObligationsModule = "Obligaciones";
    public const string MaintenanceModule = "Mantenimiento";
    public const string PayrollModule = "Nómina de colaboradores";
    public const string MonthlySummaryModule = "Resumen mensual";
    public const string AnnualBalanceModule = "Balance anual";

    [ObservableProperty]
    private string title = LocalUseModule;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string selectedAction = string.Empty;

    [ObservableProperty]
    private OperationRow? selectedRow;

    [ObservableProperty]
    private SimpleFinancialRow? selectedSimpleFinancialRow;

    [ObservableProperty]
    private string primaryText = string.Empty;

    [ObservableProperty]
    private string secondaryText = string.Empty;

    [ObservableProperty]
    private string extraText = string.Empty;

    [ObservableProperty]
    private string dateText = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [ObservableProperty]
    private string endDateText = string.Empty;

    [ObservableProperty]
    private string amountText = string.Empty;

    [ObservableProperty]
    private string secondaryAmountText = string.Empty;

    [ObservableProperty]
    private string quantityText = string.Empty;

    [ObservableProperty]
    private string optionalDescriptionText = string.Empty;

    [ObservableProperty]
    private EntityOption? selectedEntityOption;

    [ObservableProperty]
    private EntityOption? selectedSecondaryEntityOption;

    [ObservableProperty]
    private string selectedPeriod = "Hoy";

    [ObservableProperty]
    private DateTime? customPeriodFrom = DateTime.Today;

    [ObservableProperty]
    private DateTime? customPeriodThrough = DateTime.Today;

    [ObservableProperty]
    private bool showCustomPeriod;

    [ObservableProperty]
    private bool showLocalUseSummary;

    [ObservableProperty]
    private int totalChairs;

    [ObservableProperty]
    private int currentHairdressers;

    [ObservableProperty]
    private int availableChairs;

    [ObservableProperty]
    private string selectedProductAvailability = string.Empty;

    [ObservableProperty]
    private string productSearchText = string.Empty;

    [ObservableProperty]
    private bool showProductSearch;

    [ObservableProperty]
    private string productSelectionExplanation = string.Empty;

    [ObservableProperty]
    private bool showCharts;

    [ObservableProperty]
    private bool isAmountReadOnly;

    [ObservableProperty]
    private bool showOptionalDescription = true;

    [ObservableProperty]
    private string calculatedTotalText = string.Empty;

    [ObservableProperty]
    private bool showCollaboratorHistory;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isError;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isFormVisible = true;

    [ObservableProperty]
    private bool confirmDelete;

    [ObservableProperty]
    private DateTime? formDate = DateTime.Today;

    [ObservableProperty]
    private DateTime? formEndDate;

    [ObservableProperty]
    private string primaryLabel = "Nombre o concepto";

    [ObservableProperty]
    private string secondaryLabel = "Tipo o categoría";

    [ObservableProperty]
    private string extraLabel = "Unidad o repetición";

    [ObservableProperty]
    private string dateLabel = "Fecha";

    [ObservableProperty]
    private string endDateLabel = "Fecha opcional";

    [ObservableProperty]
    private string amountLabel = "Valor";

    [ObservableProperty]
    private string secondaryAmountLabel = "Costo real";

    [ObservableProperty]
    private string quantityLabel = "Cantidad";

    [ObservableProperty]
    private bool showPrimary = true;

    [ObservableProperty]
    private bool showSecondary;

    [ObservableProperty]
    private bool showExtra;

    [ObservableProperty]
    private bool showDate = true;

    [ObservableProperty]
    private bool showEndDate;

    [ObservableProperty]
    private bool showAmount;

    [ObservableProperty]
    private bool showSecondaryAmount;

    [ObservableProperty]
    private bool showQuantity;

    [ObservableProperty]
    private bool usePrimarySelector;

    [ObservableProperty]
    private bool useSecondarySelector;

    [ObservableProperty]
    private bool hasRecoveredDraft;

    [ObservableProperty]
    private bool showCommitAction = true;

    [ObservableProperty]
    private bool showActionSelector = true;

    [ObservableProperty]
    private bool showRecordActions = true;

    public ObservableCollection<string> PrimaryOptions { get; } = [];

    public ObservableCollection<EntityOption> EntityOptions { get; } = [];

    public ObservableCollection<EntityOption> SecondaryEntityOptions { get; } = [];

    public ObservableCollection<string> SecondaryOptions { get; } = [];

    public ObservableCollection<string> ExtraOptions { get; } = [];

    public ObservableCollection<string> ActionOptions { get; } = [];

    public ObservableCollection<OperationRow> Rows { get; } = [];

    public ObservableCollection<OperationRow> ActivityRows { get; } = [];

    public ObservableCollection<OperationRow> CollaboratorHistoryRows { get; } = [];

    public ObservableCollection<SimpleFinancialRow> SimpleFinancialRows { get; } = [];

    public bool ShowSimpleFinancialTable => Title is OtherIncomeModule or ExpensesModule or UnexpectedModule;

    public bool ShowGeneralRecordsTable => !ShowSimpleFinancialTable;

    public ObservableCollection<string> PeriodOptions { get; } =
        ["Hoy", "Esta semana", "Este mes", "Últimos 3 meses", "Últimos 6 meses", "Este año", "Rango personalizado"];

    public PlotModel IncomeGoalChart { get; } = new() { Title = "Ingresos frente a meta mensual" };

    public PlotModel ExpenseCompositionChart { get; } = new() { Title = "Composición de gastos" };

    public PlotModel ResultEvolutionChart { get; } = new() { Title = "Evolución del resultado" };

    public async Task SelectModuleAsync(string module)
    {
        suppressFormTracking = true;
        HasRecoveredDraft = false;
        Title = module;
        ConfigureModule();
        ClearForm();
        suppressFormTracking = false;
        await RefreshAsync();
        await RestoreDraftAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
            if (lastObservedDay != today)
            {
                lastObservedDay = today;
                if (SelectedPeriod == "Hoy")
                {
                    CustomPeriodFrom = today.ToDateTime(TimeOnly.MinValue);
                    CustomPeriodThrough = today.ToDateTime(TimeOnly.MinValue);
                }
            }
            DateOnly throughDate = new YearMonth(today.Year, today.Month).LastDay;
            if (Title == MonthlySummaryModule && TryParseDate(DateText, out DateOnly monthlyDate))
            {
                throughDate = YearMonth.From(monthlyDate).LastDay;
            }
            else if (Title == AnnualBalanceModule && TryParseDate(DateText, out DateOnly annualDate))
            {
                throughDate = new DateOnly(annualDate.Year, 12, 31);
            }

            AdministrationData data = await service.GenerateScheduledRecordsAsync(throughDate);
            SettingsDto settings = await getSettings.ExecuteAsync();
            Rows.Clear();
            foreach (OperationRow row in BuildRows(data, settings))
            {
                Rows.Add(row);
            }

            PopulateSimpleFinancialRows(data, settings, today);

            ActivityRows.Clear();
            foreach (OperationRow row in BuildActivityRows(data, today))
            {
                ActivityRows.Add(row);
            }

            ChairCapacity capacity = HomeDashboardCalculator.Capacity(data, today);
            TotalChairs = capacity.Total;
            CurrentHairdressers = capacity.CurrentPeople;
            AvailableChairs = capacity.Available;

            PopulateSelectors(data);
            if (Title == MonthlySummaryModule)
            {
                BuildMonthlyCharts(data, settings);
            }

            StatusMessage = Rows.Count == 0 && ActivityRows.Count == 0 ? "No hay registros para mostrar." : string.Empty;
            IsError = false;
        }
        catch (Exception exception)
        {
            SetError("No fue posible cargar la información.", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PopulateSimpleFinancialRows(AdministrationData data, SettingsDto settings, DateOnly today)
    {
        SimpleFinancialRows.Clear();
        FinancialEntryType? type = Title switch
        {
            OtherIncomeModule => FinancialEntryType.OtherIncome,
            ExpensesModule => FinancialEntryType.Expense,
            UnexpectedModule => FinancialEntryType.UnexpectedExpense,
            _ => null,
        };
        if (!type.HasValue) return;
        ActivityPeriod period = SelectedPeriod switch
        {
            "Esta semana" => ActivityPeriod.ThisWeek,
            "Este mes" => ActivityPeriod.ThisMonth,
            "Últimos 3 meses" => ActivityPeriod.LastThreeMonths,
            "Últimos 6 meses" => ActivityPeriod.LastSixMonths,
            "Este año" => ActivityPeriod.ThisYear,
            "Rango personalizado" => ActivityPeriod.Custom,
            _ => ActivityPeriod.Today,
        };
        ActivityDateRange range = ActivityPeriodCalculator.Calculate(
            period, today,
            CustomPeriodFrom.HasValue ? DateOnly.FromDateTime(CustomPeriodFrom.Value) : null,
            CustomPeriodThrough.HasValue ? DateOnly.FromDateTime(CustomPeriodThrough.Value) : null);
        foreach (FinancialEntry entry in data.FinancialEntries
            .Where(item => item.Type == type.Value && range.Contains(item.Date))
            .OrderByDescending(item => item.Date).ThenByDescending(item => item.CreatedUtc))
        {
            SimpleFinancialRows.Add(new SimpleFinancialRow(
                entry,
                entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                entry.Concept,
                entry.Category.HasValue ? SpanishText.For(entry.Category.Value) : SpanishText.For(entry.Type),
                $"{settings.CurrencyCode} {entry.Amount.ToDecimal():N2}",
                entry.Description ?? string.Empty));
        }
        OnPropertyChanged(nameof(ShowSimpleFinancialTable));
        OnPropertyChanged(nameof(ShowGeneralRecordsTable));
    }

    partial void OnSelectedSimpleFinancialRowChanged(SimpleFinancialRow? value)
    {
        SelectedRow = value is null
            ? null
            : new OperationRow(value.Date, value.Concept, value.Category, string.Empty, value.Amount, string.Empty, value.Entry);
    }

    [RelayCommand]
    private async Task SelectActionAsync(string action)
    {
        if (string.IsNullOrWhiteSpace(action)) return;
        SelectedAction = action;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task PrepareInventoryPurchaseAsync(OperationRow row) =>
        await PrepareInventoryRowActionAsync(row, "Registrar compra");

    [RelayCommand]
    private async Task PrepareInventoryCountAsync(OperationRow row) =>
        await PrepareInventoryRowActionAsync(row, "Conteo físico");

    [RelayCommand]
    private async Task PrepareInventoryConsumptionAsync(OperationRow row) =>
        await PrepareInventoryRowActionAsync(row, "Registrar consumo");

    private async Task PrepareInventoryRowActionAsync(OperationRow row, string action)
    {
        if (row.Entity is not Product product) return;
        SelectedRow = row;
        await SelectActionAsync(action);
        SelectedEntityOption = EntityOptions.SingleOrDefault(item => item.Id == product.Id);
    }

    [RelayCommand]
    private async Task PrepareObligationPaymentAsync(OperationRow row)
    {
        if (row.Entity is not Obligation obligation) return;
        SelectedRow = row;
        await SelectActionAsync("Registrar pago");
        SelectedEntityOption = EntityOptions.SingleOrDefault(item => item.Id == obligation.Id);
    }

    [RelayCommand]
    private async Task PrepareMaintenanceCompletionAsync(OperationRow row)
    {
        if (row.Entity is not MaintenanceRecord) return;
        SelectedRow = row;
        await SelectActionAsync("Registrar realización");
    }

    [RelayCommand]
    private void PrepareEdit(OperationRow row)
    {
        SelectedRow = row;
        LoadSelected();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedAction))
        {
            StatusMessage = "Selecciona una operación.";
            IsError = true;
            return;
        }

        IsBusy = true;
        try
        {
            string draftKey = CurrentDraftKey();
            await ExecuteSelectedActionAsync(draftKey);
            StatusMessage = "La operación se guardó correctamente.";
            IsError = false;
            HasRecoveredDraft = false;
            ClearForm(keepMessage: true);
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (Exception exception)
        {
            SetError("No fue posible guardar la operación.", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void LoadSelected()
    {
        if (SelectedRow?.Entity is null)
        {
            StatusMessage = "Selecciona un registro editable.";
            IsError = true;
            return;
        }

        suppressFormTracking = true;
        SelectedAction = ActionForEntity(SelectedRow.Entity);
        ConfigureFieldPresentation();
        LoadEntity(SelectedRow.Entity);
        suppressFormTracking = false;
        isEditing = true;
        StatusMessage = "Edición activa: los cambios válidos se guardan automáticamente.";
        IsError = false;
        _ = RestoreDraftAsync();
    }

    private static string ActionForEntity(AuditableEntity entity) => entity switch
    {
        Chair => "Añadir silla",
        LocalUsePerson => "Añadir trabajador",
        LocalUsePayment => "Registrar pago",
        Collaborator => "Agregar colaborador",
        Product => "Agregar producto",
        InventoryMovement movement => movement.Type switch
        {
            InventoryMovementType.Purchase => "Registrar compra",
            InventoryMovementType.InternalConsumption => "Registrar consumo",
            InventoryMovementType.PhysicalCountAdjustment => "Conteo físico",
            InventoryMovementType.Sale => "Registrar venta",
            _ => "Agregar producto",
        },
        MonthlyRestockPlan => "Plan mensual",
        FinancialEntry entry => entry.Type switch
        {
            FinancialEntryType.OtherIncome => "Registrar ingreso",
            FinancialEntryType.Expense => "Registrar gasto",
            _ => "Registrar imprevisto",
        },
        Obligation => "Agregar obligación",
        ObligationPayment => "Registrar pago",
        MaintenanceRecord => "Programar mantenimiento",
        DistributionPayment => "Pagar distribución",
        _ => string.Empty,
    };

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (SelectedRow?.Entity is not { } entity)
        {
            StatusMessage = "Selecciona y carga un registro editable.";
            IsError = true;
            return;
        }

        IsBusy = true;
        try
        {
            await UpdateEntityAsync(entity, CurrentDraftKey());
            StatusMessage = "El registro se editó correctamente.";
            IsError = false;
            isEditing = false;
            HasRecoveredDraft = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (Exception exception)
        {
            SetError("No fue posible editar el registro.", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedRow?.Entity is not { } entity)
        {
            StatusMessage = "Selecciona un registro para eliminar.";
            IsError = true;
            return;
        }

        if (!ConfirmDelete)
        {
            StatusMessage = "Marca “Confirmo eliminar” antes de eliminar el registro.";
            IsError = true;
            return;
        }

        IsBusy = true;
        try
        {
            if (entity is InventoryMovement movement)
            {
                await service.DeleteInventoryMovementAsync(movement);
            }
            else
            {
                await service.DeleteAsync(entity);
            }

            StatusMessage = "El registro se eliminó conservando su historial.";
            IsError = false;
            ConfirmDelete = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (Exception exception)
        {
            SetError("No fue posible eliminar el registro.", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteSelectedActionAsync(string completedDraftKey)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        DateOnly date = ParseDate(DateText, "fecha");

        switch (Title, SelectedAction)
        {
            case (LocalUseModule, "Añadir silla"):
                await service.AddChairAsync(
                    Chair.Create(PrimaryText, date, OptionalDescriptionText, utcNow),
                    completedDraftKey: completedDraftKey);
                break;
            case (LocalUseModule, "Añadir trabajador"):
                await service.AddLocalUsePersonWithChairAsync(
                    LocalUsePerson.Create(PrimaryText, date, ParseOptionalDate(EndDateText), utcNow, OptionalDescriptionText),
                    RequireSecondaryOption().Id,
                    DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
                    completedDraftKey: completedDraftKey);
                break;
            case (LocalUseModule, "Registrar pago"):
                await service.RegisterLocalUsePaymentAsync(
                    RequireSelectedOption().Id, date, ParseMoney(AmountText),
                    completedDraftKey: completedDraftKey, description: OptionalDescriptionText);
                break;
            case (LocalUseModule, "Asignar o cambiar silla"):
                await service.AssignChairAsync(
                    RequireSelectedOption().Id,
                    RequireSecondaryOption().Id,
                    DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime));
                break;
            case (LocalUseModule, "Retirar silla"):
                await service.AssignChairAsync(
                    RequireSelectedOption().Id,
                    null,
                    DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime));
                break;
            case (CollaboratorsModule, "Agregar colaborador"):
                await service.AddAsync(Collaborator.Create(
                    PrimaryText, date, ParseOptionalDate(EndDateText), utcNow, OptionalDescriptionText), completedDraftKey: completedDraftKey);
                break;
            case (SalesModule, "Registrar venta"):
                await service.RegisterSaleAsync(
                    RequireSelectedOption().Id,
                    date,
                    Quantity.Positive(ParseDecimal(QuantityText, "cantidad a vender")),
                    OptionalDescriptionText,
                    completedDraftKey: completedDraftKey);
                break;
            case (InventoryModule, "Agregar producto"):
                ProductCategory category = ParseProductCategory(SecondaryText);
                Product product = Product.Create(
                    PrimaryText,
                    category,
                    "unidad",
                    utcNow,
                    ParseOptionalMoney(SecondaryAmountText),
                    OptionalDescriptionText);
                await service.AddProductWithInitialStockAsync(
                    product,
                    date,
                    Quantity.NonNegative(ParseDecimal(QuantityText, "cantidad inicial")),
                    ParseMoney(AmountText),
                    OptionalDescriptionText,
                    completedDraftKey: completedDraftKey);
                break;
            case (InventoryModule, "Registrar compra"):
                await service.RegisterPurchaseAsync(
                    RequireSelectedOption().Id,
                    date,
                    Quantity.Positive(ParseDecimal(QuantityText, "cantidad comprada")),
                    ParseMoney(AmountText),
                    OptionalDescriptionText,
                    completedDraftKey: completedDraftKey);
                break;
            case (InventoryModule, "Registrar consumo"):
                await AddConsumptionAsync(date, utcNow, completedDraftKey);
                break;
            case (InventoryModule, "Conteo físico"):
                await AddPhysicalCountAsync(date, utcNow, completedDraftKey);
                break;
            case (InventoryModule, "Plan mensual"):
                await AddRestockPlanAsync(date, utcNow, completedDraftKey);
                break;
            case (OtherIncomeModule, "Registrar ingreso"):
                await service.AddAsync(FinancialEntry.CreateIncome(
                    date, PrimaryText, ParseMoney(AmountText), utcNow, OptionalDescriptionText), completedDraftKey: completedDraftKey);
                break;
            case (ExpensesModule, "Registrar gasto"):
                await service.AddAsync(FinancialEntry.CreateExpense(
                    date, PrimaryText, ExpenseCategory.Other, ParseMoney(AmountText), utcNow, OptionalDescriptionText), completedDraftKey: completedDraftKey);
                break;
            case (UnexpectedModule, "Registrar imprevisto"):
                await service.AddAsync(FinancialEntry.CreateUnexpectedExpense(
                    date, PrimaryText, ParseMoney(AmountText), utcNow, OptionalDescriptionText), completedDraftKey: completedDraftKey);
                break;
            case (ObligationsModule, "Agregar obligación"):
                await service.AddObligationAsync(
                    Obligation.Create(
                        PrimaryText,
                        ParseObligationType(SecondaryText),
                        date,
                        ParseMoney(AmountText),
                        ParseRecurrence(ExtraText),
                        utcNow,
                        OptionalDescriptionText),
                    new YearMonth(
                        timeProvider.GetLocalNow().Year,
                        timeProvider.GetLocalNow().Month).LastDay,
                    completedDraftKey: completedDraftKey);
                break;
            case (ObligationsModule, "Registrar pago"):
                await service.AddAsync(ObligationPayment.Create(
                    RequireSelectedOption().Id, date, ParseMoney(AmountText), utcNow, OptionalDescriptionText), completedDraftKey: completedDraftKey);
                break;
            case (MaintenanceModule, "Programar mantenimiento"):
                await service.AddAsync(MaintenanceRecord.Create(
                    PrimaryText,
                    SecondaryText,
                    date,
                    ParseOptionalMoney(AmountText),
                    null,
                    null,
                    utcNow,
                    OptionalDescriptionText), completedDraftKey: completedDraftKey);
                break;
            case (MaintenanceModule, "Registrar realización"):
                MaintenanceRecord maintenance = RequireSelected<MaintenanceRecord>();
                maintenance.Update(
                    maintenance.Asset,
                    maintenance.MaintenanceType,
                    maintenance.ScheduledDate,
                    maintenance.EstimatedCost,
                    date,
                    ParseMoney(AmountText),
                    utcNow,
                    OptionalDescriptionText);
                await service.UpdateAsync(maintenance, completedDraftKey: completedDraftKey);
                break;
            case (PayrollModule, "Cerrar mes"):
                await CloseMonthAsync(date, completedDraftKey);
                break;
            case (PayrollModule, "Pagar distribución"):
                await PayDistributionAsync(date, completedDraftKey);
                break;
            case (PayrollModule, "Reabrir cierre"):
                await service.ReopenMonthAsync(RequireSelected<MonthlyClose>().Id);
                break;
            case (MonthlySummaryModule or AnnualBalanceModule, "Consultar"):
                break;
            default:
                throw new InvalidOperationException("La operación seleccionada no está disponible en este módulo.");
        }
    }

    private async Task AddSaleAsync(DateOnly date, DateTime utcNow, string completedDraftKey)
    {
        AdministrationData data = await service.LoadAsync();
        Product product = FindProduct(data, PrimaryText);
        InventoryMovement[] movements = data.InventoryMovements.Where(item => item.ProductId == product.Id).ToArray();
        InventoryMovement sale = InventoryMovement.Sale(
            product.Id,
            date,
            Quantity.Positive(ParseDecimal(QuantityText, "cantidad")),
            ParseMoney(AmountText),
            InventoryCalculator.AverageUnitCost(movements),
            InventoryCalculator.CurrentQuantity(movements),
            utcNow);
        await service.AddInventoryMovementAsync(sale, completedDraftKey: completedDraftKey);
    }

    private async Task AddInventoryEntryAsync(
        DateOnly date,
        DateTime utcNow,
        InventoryMovementType type,
        string completedDraftKey)
    {
        AdministrationData data = await service.LoadAsync();
        Product product = FindProduct(data, PrimaryText);
        Quantity quantity = Quantity.Positive(ParseDecimal(QuantityText, "cantidad"));
        Money cost = ParseMoney(AmountText);
        InventoryMovement movement = type == InventoryMovementType.InitialStock
            ? InventoryMovement.Initial(product.Id, date, quantity, cost, utcNow)
            : InventoryMovement.Purchase(product.Id, date, quantity, cost, utcNow);
        await service.AddInventoryMovementAsync(movement, completedDraftKey: completedDraftKey);
    }

    private async Task AddConsumptionAsync(DateOnly date, DateTime utcNow, string completedDraftKey)
    {
        AdministrationData data = await service.LoadAsync();
        Product product = data.Products.Single(item => item.Id == RequireSelectedOption().Id);
        decimal current = InventoryCalculator.CurrentQuantity(
            data.InventoryMovements.Where(item => item.ProductId == product.Id));
        InventoryMovement movement = InventoryMovement.Consumption(
            product.Id, date, Quantity.Positive(ParseDecimal(QuantityText, "cantidad")), current, utcNow, OptionalDescriptionText);
        await service.AddInventoryMovementAsync(movement, completedDraftKey: completedDraftKey);
    }

    private async Task AddPhysicalCountAsync(DateOnly date, DateTime utcNow, string completedDraftKey)
    {
        AdministrationData data = await service.LoadAsync();
        Product product = data.Products.Single(item => item.Id == RequireSelectedOption().Id);
        decimal current = InventoryCalculator.CurrentQuantity(
            data.InventoryMovements.Where(item => item.ProductId == product.Id));
        InventoryMovement movement = InventoryMovement.PhysicalCount(
            product.Id, date, Quantity.NonNegative(ParseDecimal(QuantityText, "cantidad física")), current, utcNow, OptionalDescriptionText);
        await service.AddInventoryMovementAsync(movement, completedDraftKey: completedDraftKey);
    }

    private async Task AddRestockPlanAsync(DateOnly date, DateTime utcNow, string completedDraftKey)
    {
        AdministrationData data = await service.LoadAsync();
        Product product = data.Products.Single(item => item.Id == RequireSelectedOption().Id);
        await service.AddAsync(MonthlyRestockPlan.Create(
            product.Id,
            YearMonth.From(date),
            Quantity.NonNegative(ParseDecimal(QuantityText, "necesidad mensual")),
            utcNow), completedDraftKey: completedDraftKey);
    }

    private async Task CloseMonthAsync(DateOnly date, string completedDraftKey)
    {
        AdministrationData data = await service.LoadAsync();
        SettingsDto settings = await getSettings.ExecuteAsync();
        YearMonth month = YearMonth.From(date);
        Guid[] participantIds = data.Collaborators
            .Where(item => item.IsCurrentOn(month.LastDay))
            .Select(item => item.Id)
            .ToArray();
        await service.CloseMonthAsync(
            month,
            BuildMonthlyInput(data, settings, month),
            Percentage.FromPercent(settings.CollaboratorProfitPercent),
            participantIds,
            completedDraftKey: completedDraftKey,
            description: OptionalDescriptionText);
    }

    private async Task PayDistributionAsync(DateOnly date, string completedDraftKey)
    {
        MonthlyCloseParticipant participant = RequireSelected<MonthlyCloseParticipant>();
        await service.RegisterDistributionPaymentAsync(
            participant.Id, date, ParseMoney(AmountText), completedDraftKey: completedDraftKey,
            description: OptionalDescriptionText);
    }

    private async Task UpdateEntityAsync(AuditableEntity entity, string completedDraftKey)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        DateOnly date = ParseDate(DateText, "fecha");
        switch (entity)
        {
            case Chair chair:
                await service.UpdateChairAsync(
                    chair.Id,
                    PrimaryText,
                    date,
                    OptionalDescriptionText,
                    completedDraftKey: completedDraftKey);
                break;
            case LocalUsePerson person:
                await service.UpdateLocalUsePersonAsync(
                    person.Id,
                    PrimaryText,
                    date,
                    ParseOptionalDate(EndDateText),
                    DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
                    completedDraftKey: completedDraftKey,
                    description: OptionalDescriptionText);
                break;
            case LocalUsePayment payment:
                AdministrationData localData = await service.LoadAsync();
                Money available = WeeklyChargeCalculator.CalculateDebt(
                    localData.WeeklyCharges.Where(item => item.PersonId == payment.PersonId),
                    localData.LocalUsePayments.Where(item => item.PersonId == payment.PersonId && item.Id != payment.Id),
                    date);
                payment.Update(date, ParseMoney(AmountText), available, utcNow, OptionalDescriptionText);
                await service.UpdateAsync(payment, completedDraftKey: completedDraftKey);
                break;
            case Collaborator collaborator:
                collaborator.Update(PrimaryText, date, ParseOptionalDate(EndDateText), utcNow, OptionalDescriptionText);
                await service.UpdateAsync(collaborator, completedDraftKey: completedDraftKey);
                break;
            case Product product:
                await service.UpdateProductAsync(
                    product.Id,
                    PrimaryText,
                    ParseProductCategory(SecondaryText),
                    "unidad",
                    completedDraftKey: completedDraftKey,
                    defaultSalePrice: ParseOptionalMoney(SecondaryAmountText),
                    description: OptionalDescriptionText);
                break;
            case InventoryMovement movement:
                movement.Correct(
                    date,
                    ParseDecimal(QuantityText, "variación de cantidad"),
                    ParseOptionalMoney(AmountText),
                    ParseOptionalMoney(SecondaryAmountText),
                    utcNow,
                    OptionalDescriptionText);
                await service.UpdateInventoryMovementAsync(movement, completedDraftKey: completedDraftKey);
                break;
            case MonthlyRestockPlan plan:
                plan.Update(
                    YearMonth.From(date),
                    Quantity.NonNegative(ParseDecimal(QuantityText, "necesidad mensual")),
                    utcNow);
                await service.UpdateAsync(plan, completedDraftKey: completedDraftKey);
                break;
            case FinancialEntry financial:
                financial.Update(
                    date,
                    PrimaryText,
                    financial.Type == FinancialEntryType.Expense ? ParseExpenseCategory(SecondaryText) : null,
                    ParseMoney(AmountText),
                    utcNow,
                    OptionalDescriptionText);
                await service.UpdateAsync(financial, completedDraftKey: completedDraftKey);
                break;
            case Obligation obligation:
                obligation.Update(
                    PrimaryText,
                    ParseObligationType(SecondaryText),
                    date,
                    ParseMoney(AmountText),
                    ParseRecurrence(ExtraText),
                    utcNow,
                    OptionalDescriptionText);
                await service.UpdateAsync(obligation, completedDraftKey: completedDraftKey);
                await service.GenerateScheduledRecordsAsync(new YearMonth(
                    timeProvider.GetLocalNow().Year,
                    timeProvider.GetLocalNow().Month).LastDay);
                break;
            case ObligationPayment obligationPayment:
                obligationPayment.Update(date, ParseMoney(AmountText), utcNow, OptionalDescriptionText);
                await service.UpdateAsync(obligationPayment, completedDraftKey: completedDraftKey);
                break;
            case MaintenanceRecord maintenance:
                maintenance.Update(
                    PrimaryText,
                    SecondaryText,
                    date,
                    ParseOptionalMoney(AmountText),
                    ParseOptionalDate(EndDateText),
                    ParseOptionalMoney(SecondaryAmountText),
                    utcNow,
                    OptionalDescriptionText);
                await service.UpdateAsync(maintenance, completedDraftKey: completedDraftKey);
                break;
            case DistributionPayment distribution:
                AdministrationData payrollData = await service.LoadAsync();
                MonthlyCloseParticipant participant = payrollData.MonthlyCloseParticipants
                    .Single(item => item.Id == distribution.ParticipantId);
                long otherPaid = payrollData.DistributionPayments
                    .Where(item => item.ParticipantId == participant.Id && item.Id != distribution.Id)
                    .Sum(item => item.Amount.MinorUnits);
                distribution.Update(
                    date,
                    ParseMoney(AmountText),
                    Money.FromMinorUnits(participant.Amount.MinorUnits - otherPaid),
                    utcNow,
                    OptionalDescriptionText);
                await service.UpdateAsync(distribution, completedDraftKey: completedDraftKey);
                break;
            default:
                throw new InvalidOperationException("Este registro es histórico o calculado y no admite edición directa.");
        }
    }

    private IEnumerable<OperationRow> BuildActivityRows(AdministrationData data, DateOnly today)
    {
        ActivityPeriod period = SelectedPeriod switch
        {
            "Esta semana" => ActivityPeriod.ThisWeek,
            "Este mes" => ActivityPeriod.ThisMonth,
            "Últimos 3 meses" => ActivityPeriod.LastThreeMonths,
            "Últimos 6 meses" => ActivityPeriod.LastSixMonths,
            "Este año" => ActivityPeriod.ThisYear,
            "Rango personalizado" => ActivityPeriod.Custom,
            _ => ActivityPeriod.Today,
        };
        DateOnly? customFrom = CustomPeriodFrom.HasValue ? DateOnly.FromDateTime(CustomPeriodFrom.Value) : null;
        DateOnly? customThrough = CustomPeriodThrough.HasValue ? DateOnly.FromDateTime(CustomPeriodThrough.Value) : null;
        ActivityDateRange range = ActivityPeriodCalculator.Calculate(period, today, customFrom, customThrough);
        string[] modules = Title switch
        {
            PayrollModule => ["Colaboradores", "Resumen mensual"],
            MonthlySummaryModule => ["Resumen mensual"],
            _ => [Title],
        };
        return data.ActivityRecords
            .Where(item => modules.Contains(item.Module) && range.Contains(item.ActivityDate))
            .OrderByDescending(item => item.OccurredUtc)
            .Select(item => Row(
                item.ActivityDate,
                item.Summary,
                item.Description ?? string.Empty,
                string.Empty,
                string.Empty,
                item.Action,
                null));
    }

    private IEnumerable<OperationRow> BuildRows(AdministrationData data, SettingsDto settings) => Title switch
    {
        LocalUseModule => BuildLocalUseRows(data, settings),
        CollaboratorsModule => data.Collaborators.Select(item => Row(
            item.StartDate, item.Name, "Colaborador", string.Empty, string.Empty,
            item.IsCurrentOn(DateOnly.FromDateTime(DateTime.Today)) ? "Vigente" : "Retirado", item)),
        SalesModule => BuildInventoryMovementRows(data, InventoryMovementType.Sale),
        InventoryModule => BuildInventoryRows(data),
        OtherIncomeModule => BuildFinancialRows(data, FinancialEntryType.OtherIncome),
        ExpensesModule => BuildFinancialRows(data, FinancialEntryType.Expense),
        UnexpectedModule => BuildFinancialRows(data, FinancialEntryType.UnexpectedExpense),
        ObligationsModule => BuildObligationRows(data),
        MaintenanceModule => data.MaintenanceRecords.Select(item => Row(
            item.ScheduledDate, item.Asset, item.MaintenanceType, string.Empty,
            FormatMoney(item.ActualCost ?? item.EstimatedCost, settings.CurrencyCode),
            item.NeedsAttention(DateOnly.FromDateTime(DateTime.Today)) ? "Pendiente" : "Programado o realizado", item)),
        PayrollModule => BuildPayrollRows(data, settings),
        MonthlySummaryModule => BuildMonthlySummaryRows(data, settings),
        AnnualBalanceModule => BuildAnnualRows(data, settings),
        _ => [],
    };

    private static IEnumerable<OperationRow> BuildLocalUseRows(AdministrationData data, SettingsDto settings)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        foreach (Chair chair in data.Chairs)
        {
            string assigned = chair.AssignedPersonId.HasValue
                ? data.LocalUsePeople.SingleOrDefault(item => item.Id == chair.AssignedPersonId)?.Name ?? "Trabajador no disponible"
                : "Disponible";
            yield return Row(chair.CreationDate, chair.Name, chair.Description ?? string.Empty,
                string.Empty, string.Empty, assigned, chair);
        }

        foreach (LocalUsePerson person in data.LocalUsePeople)
        {
            Money debt = WeeklyChargeCalculator.CalculateDebt(
                data.WeeklyCharges.Where(item => item.PersonId == person.Id),
                data.LocalUsePayments.Where(item => item.PersonId == person.Id),
                today);
            string chair = data.Chairs.SingleOrDefault(item => item.AssignedPersonId == person.Id)?.Name ?? "Sin silla";
            yield return Row(
                person.EntryDate, person.Name, $"Trabajador · {chair}", string.Empty,
                FormatMoney(debt, settings.CurrencyCode), debt.MinorUnits > 0 ? "Con deuda" : "Al día", person);
        }

        foreach (LocalUsePayment payment in data.LocalUsePayments)
        {
            string name = data.LocalUsePeople.SingleOrDefault(item => item.Id == payment.PersonId)?.Name ?? "Persona eliminada";
            yield return Row(
                payment.PaymentDate, name, "Pago recibido", string.Empty,
                FormatMoney(payment.Amount, settings.CurrencyCode), "Registrado", payment);
        }
    }

    private static IEnumerable<OperationRow> BuildInventoryRows(AdministrationData data)
    {
        foreach (Product product in data.Products)
        {
            decimal current = InventoryCalculator.CurrentQuantity(
                data.InventoryMovements.Where(item => item.ProductId == product.Id));
            yield return Row(
                null, product.Name, ProductCategoryName(product.Category),
                current.ToString("0.###", CultureInfo.CurrentCulture),
                product.DefaultSalePrice?.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture) ?? string.Empty,
                "Existencia actual", product);
        }

        foreach (OperationRow row in BuildInventoryMovementRows(data, null))
        {
            yield return row;
        }

        foreach (MonthlyRestockPlan plan in data.RestockPlans)
        {
            string product = data.Products.SingleOrDefault(item => item.Id == plan.ProductId)?.Name ?? "Producto eliminado";
            yield return Row(
                plan.Month.FirstDay, product, "Plan mensual", plan.NeededQuantity.Value.ToString("0.###"),
                string.Empty, plan.Month.ToString(), plan);
        }
    }

    private static IEnumerable<OperationRow> BuildInventoryMovementRows(
        AdministrationData data,
        InventoryMovementType? onlyType) => data.InventoryMovements
        .Where(item => !onlyType.HasValue || item.Type == onlyType)
        .Select(item => Row(
            item.Date,
            data.Products.SingleOrDefault(product => product.Id == item.ProductId)?.Name ?? "Producto eliminado",
            MovementTypeName(item.Type),
            item.QuantityDelta.ToString("0.###", CultureInfo.CurrentCulture),
            item.CashAmount.HasValue ? item.CashAmount.Value.ToDecimal().ToString("0.00") : string.Empty,
            "Registrado",
            item));

    private static IEnumerable<OperationRow> BuildFinancialRows(
        AdministrationData data,
        FinancialEntryType type) => data.FinancialEntries
        .Where(item => item.Type == type)
        .Select(item => Row(
            item.Date, item.Concept, item.Category.HasValue ? SpanishText.For(item.Category.Value) : SpanishText.For(type), string.Empty,
            item.Amount.ToDecimal().ToString("0.00"), "Registrado", item));

    private static IEnumerable<OperationRow> BuildObligationRows(AdministrationData data)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        foreach (Obligation item in data.Obligations)
        {
            ObligationPayment[] payments = data.ObligationPayments
                .Where(payment => payment.ObligationId == item.Id)
                .ToArray();
            yield return Row(
                item.DueDate, item.Name, $"{SpanishText.For(item.Type)} · {SpanishText.For(item.Recurrence)}", string.Empty,
                item.GoalAmount(payments).ToDecimal().ToString("0.00"), SpanishText.For(item.Status(payments, today)), item);
        }

        foreach (ObligationPayment payment in data.ObligationPayments)
        {
            string name = data.Obligations.SingleOrDefault(item => item.Id == payment.ObligationId)?.Name
                ?? "Obligación eliminada";
            yield return Row(
                payment.Date, name, "Pago de obligación", string.Empty,
                payment.Amount.ToDecimal().ToString("0.00"), "Registrado", payment);
        }
    }

    private static IEnumerable<OperationRow> BuildPayrollRows(AdministrationData data, SettingsDto settings)
    {
        foreach (MonthlyClose close in data.MonthlyCloses.OrderByDescending(item => item.Month.Year).ThenByDescending(item => item.Month.Month))
        {
            yield return Row(
                close.Month.FirstDay, "Cierre mensual", $"Fondo · {close.CollaboratorPercentageBasisPoints / 100m:0.##}%",
                string.Empty, FormatMinorUnits(close.FundMinorUnits, settings.CurrencyCode),
                close.IsConfirmed ? "Confirmado" : "Reabierto", close);
        }

        foreach (MonthlyCloseParticipant participant in data.MonthlyCloseParticipants)
        {
            string name = data.Collaborators.SingleOrDefault(item => item.Id == participant.CollaboratorId)?.Name ?? "Colaborador eliminado";
            MonthlyClose? close = data.MonthlyCloses.SingleOrDefault(item => item.Id == participant.CloseId);
            if (close is null || !close.IsConfirmed)
            {
                continue;
            }

            long paid = data.DistributionPayments.Where(item => item.ParticipantId == participant.Id).Sum(item => item.Amount.MinorUnits);
            yield return Row(
                close.Month.FirstDay, name, $"Asignación · {close.Month}", string.Empty,
                FormatMinorUnits(participant.Amount.MinorUnits, settings.CurrencyCode),
                paid >= participant.Amount.MinorUnits ? "Pagado" : $"Pendiente {FormatMinorUnits(participant.Amount.MinorUnits - paid, settings.CurrencyCode)}",
                participant);
        }

        foreach (DistributionPayment payment in data.DistributionPayments)
        {
            MonthlyCloseParticipant? participant = data.MonthlyCloseParticipants
                .SingleOrDefault(item => item.Id == payment.ParticipantId);
            MonthlyClose? close = participant is null
                ? null
                : data.MonthlyCloses.SingleOrDefault(item => item.Id == participant.CloseId);
            if (participant is null || close is null || !close.IsConfirmed)
            {
                continue;
            }

            string collaborator = data.Collaborators
                .SingleOrDefault(item => item.Id == participant.CollaboratorId)?.Name ?? "Colaborador eliminado";
            yield return Row(
                payment.Date, collaborator, $"Pago de distribución · {close.Month}", string.Empty,
                FormatMoney(payment.Amount, settings.CurrencyCode), "Registrado", payment);
        }
    }

    private IEnumerable<OperationRow> BuildMonthlySummaryRows(AdministrationData data, SettingsDto settings)
    {
        YearMonth month = YearMonth.From(ParseDate(DateText, "mes a consultar"));
        MonthlySummaryResult result = AdministrationReports.MonthlySummary(
            data,
            Money.FromDecimal(settings.OptionalSuppliesMonthlyBudget),
            Percentage.FromPercent(settings.CollaboratorProfitPercent),
            month);
        return
        [
            SummaryRow(month, "Ingresos", result.IncomeMinorUnits, settings.CurrencyCode),
            SummaryRow(month, "Meta mensual", result.GoalMinorUnits, settings.CurrencyCode),
            SummaryRow(month, "Faltante", result.MissingMinorUnits, settings.CurrencyCode),
            SummaryRow(month, "Resultado base", result.BaseResultMinorUnits, settings.CurrencyCode),
            SummaryRow(month, "Fondo colaboradores", result.CollaboratorFundMinorUnits, settings.CurrencyCode),
            SummaryRow(month, "Resultado retenido", result.RetainedResultMinorUnits, settings.CurrencyCode),
        ];
    }

    private void BuildMonthlyCharts(AdministrationData data, SettingsDto settings)
    {
        YearMonth month = YearMonth.From(ParseDate(DateText, "mes a consultar"));
        Money optionalBudget = Money.FromDecimal(settings.OptionalSuppliesMonthlyBudget);
        Percentage percentage = Percentage.FromPercent(settings.CollaboratorProfitPercent);
        MonthlySummaryResult result = AdministrationReports.MonthlySummary(data, optionalBudget, percentage, month);
        MonthlyExpenseBreakdown expenses = AdministrationReports.MonthlyExpenses(data, optionalBudget, month);

        IncomeGoalChart.Series.Clear();
        IncomeGoalChart.Axes.Clear();
        var categoryAxis = new CategoryAxis { Position = AxisPosition.Left };
        categoryAxis.Labels.Add("Ingresos");
        categoryAxis.Labels.Add("Meta mensual");
        IncomeGoalChart.Axes.Add(categoryAxis);
        double incomeGoalMaximum = Math.Max(Math.Abs(result.IncomeMinorUnits / 100d), Math.Abs(result.GoalMinorUnits / 100d));
        IncomeGoalChart.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = 0,
            Title = settings.CurrencyCode,
            LabelFormatter = FormatChartAxisValue,
            MajorStep = ChartMajorStep(incomeGoalMaximum),
            MaximumPadding = 0.08,
        });
        var columns = new BarSeries
        {
            FillColor = OxyColor.FromRgb(46, 91, 255),
            TrackerFormatString = "{0}\n{1}: {2}\n{3}: {4:N0}",
        };
        columns.Items.Add(new BarItem(result.IncomeMinorUnits / 100d));
        columns.Items.Add(new BarItem(result.GoalMinorUnits / 100d));
        IncomeGoalChart.Series.Add(columns);
        IncomeGoalChart.InvalidatePlot(true);

        ExpenseCompositionChart.Series.Clear();
        ExpenseCompositionChart.Axes.Clear();
        var pie = new PieSeries
        {
            StrokeThickness = 1,
            InsideLabelPosition = 0.65,
            OutsideLabelFormat = "{1}: {0:N0}",
            Diameter = 0.62,
        };
        AddSlice(pie, "Servicios", expenses.ServicesMinorUnits, OxyColor.FromRgb(37, 99, 235));
        AddSlice(pie, "Impuestos", expenses.TaxesMinorUnits, OxyColor.FromRgb(124, 58, 237));
        AddSlice(pie, "Otras obligaciones", expenses.OtherObligationsMinorUnits, OxyColor.FromRgb(14, 116, 144));
        AddSlice(pie, "Mercancía", expenses.MerchandiseMinorUnits, OxyColor.FromRgb(5, 150, 105));
        AddSlice(pie, "Insumos", expenses.MandatorySuppliesMinorUnits + expenses.OptionalSuppliesMinorUnits, OxyColor.FromRgb(101, 163, 13));
        AddSlice(pie, "Mantenimiento", expenses.MaintenanceMinorUnits, OxyColor.FromRgb(217, 119, 6));
        AddSlice(pie, "Imprevistos", expenses.UnexpectedMinorUnits, OxyColor.FromRgb(220, 38, 38));
        AddSlice(pie, "Otros", expenses.OtherExpensesMinorUnits + expenses.PendingPlansMinorUnits, OxyColor.FromRgb(71, 85, 105));
        if (pie.Slices.Count == 0)
        {
            pie.Slices.Add(new PieSlice("Sin datos", 1) { Fill = OxyColors.LightGray });
        }
        ExpenseCompositionChart.Series.Add(pie);
        ExpenseCompositionChart.InvalidatePlot(true);

        ResultEvolutionChart.Series.Clear();
        ResultEvolutionChart.Axes.Clear();
        var monthsAxis = new CategoryAxis { Position = AxisPosition.Bottom, Angle = -35 };
        var resultAxis = new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = settings.CurrencyCode,
            LabelFormatter = FormatChartAxisValue,
            MaximumPadding = 0.08,
            MinimumPadding = 0.08,
        };
        var line = new LineSeries
        {
            Title = "Resultado retenido",
            Color = OxyColor.FromRgb(5, 150, 105),
            MarkerType = MarkerType.Circle,
            MarkerFill = OxyColor.FromRgb(5, 150, 105),
            TrackerFormatString = "{0}\n{1}: {2}\n{3}: {4:N0}",
        };
        DateOnly firstMonth = month.FirstDay.AddMonths(-11);
        double resultMaximum = 0d;
        for (int index = 0; index < 12; index++)
        {
            DateOnly date = firstMonth.AddMonths(index);
            YearMonth itemMonth = YearMonth.From(date);
            MonthlySummaryResult item = AdministrationReports.MonthlySummary(data, optionalBudget, percentage, itemMonth);
            monthsAxis.Labels.Add($"{itemMonth.Month:00}/{itemMonth.Year}");
            double value = item.RetainedResultMinorUnits / 100d;
            line.Points.Add(new DataPoint(index, value));
            resultMaximum = Math.Max(resultMaximum, Math.Abs(value));
        }
        resultAxis.MajorStep = ChartMajorStep(resultMaximum);
        ResultEvolutionChart.Axes.Add(monthsAxis);
        ResultEvolutionChart.Axes.Add(resultAxis);
        ResultEvolutionChart.Series.Add(line);
        ResultEvolutionChart.InvalidatePlot(true);
    }

    public static string FormatChartAxisValue(double value) =>
        value.ToString("N0", CultureInfo.GetCultureInfo("es-CO"));

    private static double ChartMajorStep(double maximum) => maximum > 0d ? maximum / 2d : 1d;

    private static void AddSlice(PieSeries pie, string label, long minorUnits, OxyColor color)
    {
        if (minorUnits > 0)
        {
            pie.Slices.Add(new PieSlice(label, minorUnits / 100d) { Fill = color });
        }
    }

    private IEnumerable<OperationRow> BuildAnnualRows(AdministrationData data, SettingsDto settings)
    {
        int year = ParseDate(DateText, "año a consultar").Year;
        AnnualAdministrationReport report = AdministrationReports.Annual(
            data,
            Money.FromDecimal(settings.OptionalSuppliesMonthlyBudget),
            Percentage.FromPercent(settings.CollaboratorProfitPercent),
            year);
        AnnualBalanceResult annual = report.Balance;
        MonthlyExpenseBreakdown expenses = report.Expenses;
        YearMonth january = new(year, 1);
        return
        [
            SummaryRow(january, "Ingresos acumulados", annual.IncomeMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Meta y gastos acumulados", annual.ExpenseMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Distribuciones pagadas", annual.DistributionMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Resultado retenido", annual.RetainedMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Pendientes", annual.PendingMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Faltante anual", annual.MissingMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Servicios", expenses.ServicesMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Impuestos", expenses.TaxesMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Otras obligaciones", expenses.OtherObligationsMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Mercancía para venta", expenses.MerchandiseMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Insumos obligatorios", expenses.MandatorySuppliesMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Insumos opcionales", expenses.OptionalSuppliesMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Mantenimiento", expenses.MaintenanceMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Imprevistos", expenses.UnexpectedMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Otros gastos", expenses.OtherExpensesMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Planes de reposición", expenses.PendingPlansMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Ajuste histórico de cierres", expenses.HistoricalAdjustmentMinorUnits, settings.CurrencyCode),
            Row(january.FirstDay, "Indicador", report.Indicator, string.Empty, string.Empty, report.Indicator, null),
        ];
    }

    internal static MonthlySummaryInput BuildMonthlyInput(
        AdministrationData data,
        SettingsDto settings,
        YearMonth month) => AdministrationReports.BuildMonthlyInput(
            data,
            Money.FromDecimal(settings.OptionalSuppliesMonthlyBudget),
            month);

    [RelayCommand]
    private async Task ClearFormFieldsAsync()
    {
        string key = CurrentDraftKey();
        editAutosaveCancellation?.Cancel();
        await formDraftStore.DeleteAsync(key);
        suppressFormTracking = true;
        isEditing = false;
        ClearForm();
        suppressFormTracking = false;
        HasRecoveredDraft = false;
        StatusMessage = "Se limpiaron únicamente los campos no registrados.";
        IsError = false;
    }

    public async Task FlushPendingAsync()
    {
        editAutosaveCancellation?.Cancel();
        await draftWriteLock.WaitAsync();
        draftWriteLock.Release();
    }

    private void TrackFormChange()
    {
        if (suppressFormTracking || string.IsNullOrWhiteSpace(SelectedAction)
            || Title is MonthlySummaryModule or AnnualBalanceModule)
        {
            return;
        }

        HasRecoveredDraft = HasFormContent();
        if (!isEditing && !HasRecoveredDraft)
        {
            return;
        }

        _ = PersistDraftSafelyAsync();
        if (isEditing)
        {
            ScheduleEditAutosave();
        }
    }

    private bool HasFormContent() => !string.IsNullOrWhiteSpace(PrimaryText)
        || !string.IsNullOrWhiteSpace(SecondaryText)
        || !string.IsNullOrWhiteSpace(ExtraText)
        || !string.IsNullOrWhiteSpace(EndDateText)
        || !string.IsNullOrWhiteSpace(AmountText)
        || !string.IsNullOrWhiteSpace(SecondaryAmountText)
        || !string.IsNullOrWhiteSpace(QuantityText)
        || !string.IsNullOrWhiteSpace(OptionalDescriptionText)
        || SelectedEntityOption is not null
        || SelectedSecondaryEntityOption is not null;

    private async Task UpdateSelectedProductDetailsAsync(EntityOption? option)
    {
        if (option is null || Title is not (SalesModule or InventoryModule))
        {
            SelectedProductAvailability = string.Empty;
            return;
        }

        AdministrationData data = await service.LoadAsync();
        Product? product = data.Products.SingleOrDefault(item => item.Id == option.Id);
        if (product is null) return;
        decimal available = InventoryCalculator.CurrentQuantity(
            data.InventoryMovements.Where(item => item.ProductId == product.Id));
        SelectedProductAvailability = Title == SalesModule
            ? $"Existencia disponible: {available:0.###} · Precio predeterminado: "
                + (product.DefaultSalePrice?.ToDecimal().ToString("N2", CultureInfo.CurrentCulture) ?? "Sin precio configurado")
            : $"Existencia disponible: {available:0.###}";
        if (Title == SalesModule)
        {
            AmountText = product.DefaultSalePrice?.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture) ?? string.Empty;
            QuantityText = string.Empty;
        }
    }

    private void UpdateCalculatedTotal()
    {
        CalculatedTotalText = string.Empty;
        if (Title != InventoryModule || SelectedAction != "Registrar compra") return;
        bool validUnitCost = decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal unitCost)
            || decimal.TryParse(AmountText, NumberStyles.Number, CultureInfo.InvariantCulture, out unitCost);
        bool validQuantity = decimal.TryParse(QuantityText, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal quantity)
            || decimal.TryParse(QuantityText, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity);
        if (validUnitCost && validQuantity)
        {
            CalculatedTotalText = $"Total calculado: {unitCost * quantity:N2}";
        }
    }

    private async Task PersistDraftSafelyAsync()
    {
        await draftWriteLock.WaitAsync();
        try
        {
            string payload = JsonSerializer.Serialize(new FormPayload(
                PrimaryText, SecondaryText, ExtraText, DateText, EndDateText,
                AmountText, SecondaryAmountText, QuantityText, OptionalDescriptionText,
                SelectedEntityOption?.Id, SelectedSecondaryEntityOption?.Id));
            Guid? entityId = isEditing ? SelectedRow?.Entity?.Id : null;
            await formDraftStore.UpsertAsync(FormDraft.Create(
                CurrentDraftKey(), Title, SelectedAction, payload, entityId, isEditing,
                timeProvider.GetUtcNow().UtcDateTime));
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible conservar los campos escritos: {exception.Message}";
            IsError = true;
        }
        finally
        {
            draftWriteLock.Release();
        }
    }

    private async Task RestoreDraftAsync()
    {
        HasRecoveredDraft = false;
        if (string.IsNullOrWhiteSpace(SelectedAction)) return;
        FormDraft? draft = await formDraftStore.FindAsync(CurrentDraftKey());
        if (draft is null) return;
        FormPayload? payload = JsonSerializer.Deserialize<FormPayload>(draft.PayloadJson);
        if (payload is null) return;

        suppressFormTracking = true;
        PrimaryText = payload.PrimaryText;
        SecondaryText = payload.SecondaryText;
        ExtraText = payload.ExtraText;
        DateText = payload.DateText;
        EndDateText = payload.EndDateText;
        AmountText = payload.AmountText;
        SecondaryAmountText = payload.SecondaryAmountText;
        QuantityText = payload.QuantityText;
        OptionalDescriptionText = payload.OptionalDescriptionText;
        SelectedEntityOption = EntityOptions.SingleOrDefault(item => item.Id == payload.SelectedEntityId);
        SelectedSecondaryEntityOption = SecondaryEntityOptions.SingleOrDefault(item => item.Id == payload.SelectedSecondaryEntityId);
        suppressFormTracking = false;
        HasRecoveredDraft = HasFormContent();
        StatusMessage = string.Empty;
        IsError = false;
    }

    private void ScheduleEditAutosave()
    {
        editAutosaveCancellation?.Cancel();
        editAutosaveCancellation = new CancellationTokenSource();
        CancellationToken token = editAutosaveCancellation.Token;
        _ = AutosaveEditAsync(token);
    }

    private async Task AutosaveEditAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(650, cancellationToken);
            if (SelectedRow?.Entity is not { } entity || !isEditing) return;
            await draftWriteLock.WaitAsync(cancellationToken);
            draftWriteLock.Release();
            await UpdateEntityAsync(entity, CurrentDraftKey());
            HasRecoveredDraft = false;
            StatusMessage = "Cambios guardados automáticamente.";
            IsError = false;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (Exception exception)
        {
            SetError("No fue posible autoguardar la edición; los campos escritos se conservaron.", exception);
        }
    }

    partial void OnPrimaryTextChanged(string value) => TrackFormChange();
    partial void OnSecondaryTextChanged(string value) => TrackFormChange();
    partial void OnExtraTextChanged(string value) => TrackFormChange();
    partial void OnAmountTextChanged(string value)
    {
        TrackFormChange();
        UpdateCalculatedTotal();
    }
    partial void OnSecondaryAmountTextChanged(string value) => TrackFormChange();
    partial void OnQuantityTextChanged(string value)
    {
        TrackFormChange();
        UpdateCalculatedTotal();
    }
    partial void OnOptionalDescriptionTextChanged(string value) => TrackFormChange();
    partial void OnSelectedEntityOptionChanged(EntityOption? value)
    {
        TrackFormChange();
        _ = UpdateSelectedProductDetailsAsync(value);
        _ = UpdateChairOptionsAsync(value);
    }

    partial void OnProductSearchTextChanged(string value)
    {
        if (Title == SalesModule)
        {
            _ = RefreshProductOptionsAsync();
        }
    }

    private async Task RefreshProductOptionsAsync()
    {
        AdministrationData data = await service.LoadAsync();
        Guid? selectedId = SelectedEntityOption?.Id;
        EntityOptions.Clear();
        foreach (Product product in data.Products
            .Where(item => item.IsForSale)
            .Where(item => string.IsNullOrWhiteSpace(ProductSearchText)
                || item.Name.Contains(ProductSearchText.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Name))
        {
            EntityOptions.Add(new EntityOption(product.Id, product.Name));
        }

        SelectedEntityOption = selectedId.HasValue
            ? EntityOptions.SingleOrDefault(item => item.Id == selectedId.Value)
            : null;
    }

    private async Task UpdateChairOptionsAsync(EntityOption? person)
    {
        if (Title != LocalUseModule || SelectedAction != "Asignar o cambiar silla") return;
        AdministrationData data = await service.LoadAsync();
        SecondaryEntityOptions.Clear();
        foreach (Chair chair in data.Chairs
            .Where(item => !item.AssignedPersonId.HasValue || item.AssignedPersonId == person?.Id)
            .OrderBy(item => item.Name))
        {
            SecondaryEntityOptions.Add(new EntityOption(chair.Id, chair.Name));
        }
    }

    private async Task LoadCollaboratorHistoryAsync()
    {
        CollaboratorHistoryRows.Clear();
        if (Title != CollaboratorsModule || SelectedRow?.Entity is not Collaborator collaborator) return;
        AdministrationData data = await service.LoadAsync();
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        ActivityPeriod period = SelectedPeriod switch
        {
            "Esta semana" => ActivityPeriod.ThisWeek,
            "Este mes" => ActivityPeriod.ThisMonth,
            "Últimos 3 meses" => ActivityPeriod.LastThreeMonths,
            "Últimos 6 meses" => ActivityPeriod.LastSixMonths,
            "Este año" => ActivityPeriod.ThisYear,
            "Rango personalizado" => ActivityPeriod.Custom,
            _ => ActivityPeriod.Today,
        };
        ActivityDateRange range = ActivityPeriodCalculator.Calculate(
            period,
            today,
            CustomPeriodFrom.HasValue ? DateOnly.FromDateTime(CustomPeriodFrom.Value) : null,
            CustomPeriodThrough.HasValue ? DateOnly.FromDateTime(CustomPeriodThrough.Value) : null);
        foreach (MonthlyCloseParticipant participant in data.MonthlyCloseParticipants
            .Where(item => item.CollaboratorId == collaborator.Id))
        {
            MonthlyClose? close = data.MonthlyCloses.SingleOrDefault(item => item.Id == participant.CloseId && item.IsConfirmed);
            if (close is null || !range.Contains(close.Month.FirstDay)) continue;
            DistributionPayment[] payments = data.DistributionPayments
                .Where(item => item.ParticipantId == participant.Id)
                .OrderBy(item => item.Date)
                .ToArray();
            long paid = payments.Sum(item => item.Amount.MinorUnits);
            CollaboratorHistoryRows.Add(Row(
                close.Month.FirstDay,
                close.Month.ToString(),
                $"Porcentaje aplicado: {close.CollaboratorPercentageBasisPoints / 100m:0.##}%",
                string.Empty,
                participant.Amount.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture),
                $"Pagado {paid / 100m:N2} · Pendiente {(participant.Amount.MinorUnits - paid) / 100m:N2}",
                participant));
            foreach (DistributionPayment payment in payments.Where(item => range.Contains(item.Date)))
            {
                CollaboratorHistoryRows.Add(Row(
                    payment.Date,
                    "Pago recibido",
                    payment.Description ?? string.Empty,
                    string.Empty,
                    payment.Amount.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture),
                    "Pagado",
                    payment));
            }
        }
    }
    partial void OnSelectedSecondaryEntityOptionChanged(EntityOption? value) => TrackFormChange();

    partial void OnSelectedPeriodChanged(string value)
    {
        ShowCustomPeriod = value == "Rango personalizado";
        _ = RefreshAsync();
        _ = LoadCollaboratorHistoryAsync();
    }

    partial void OnSelectedRowChanged(OperationRow? value) => _ = LoadCollaboratorHistoryAsync();

    partial void OnCustomPeriodFromChanged(DateTime? value)
    {
        if (ShowCustomPeriod)
        {
            _ = RefreshAsync();
            _ = LoadCollaboratorHistoryAsync();
        }
    }

    partial void OnCustomPeriodThroughChanged(DateTime? value)
    {
        if (ShowCustomPeriod)
        {
            _ = RefreshAsync();
            _ = LoadCollaboratorHistoryAsync();
        }
    }

    partial void OnDateTextChanged(string value)
    {
        if (TryParseDate(value, out DateOnly date)) FormDate = date.ToDateTime(TimeOnly.MinValue);
        TrackFormChange();
        if (Title is MonthlySummaryModule or AnnualBalanceModule) _ = RefreshAsync();
    }

    partial void OnEndDateTextChanged(string value)
    {
        FormEndDate = TryParseDate(value, out DateOnly date) ? date.ToDateTime(TimeOnly.MinValue) : null;
        TrackFormChange();
    }

    partial void OnFormDateChanged(DateTime? value)
    {
        if (value.HasValue) DateText = DateOnly.FromDateTime(value.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    partial void OnFormEndDateChanged(DateTime? value) => EndDateText = value.HasValue
        ? DateOnly.FromDateTime(value.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        : string.Empty;

    partial void OnSelectedActionChanged(string value)
    {
        if (suppressFormTracking) return;
        editAutosaveCancellation?.Cancel();
        isEditing = false;
        suppressFormTracking = true;
        ClearForm();
        ConfigureFieldPresentation();
        suppressFormTracking = false;
        _ = RestoreDraftAsync();
    }

    private string CurrentDraftKey()
    {
        string suffix = isEditing && SelectedRow?.Entity is { } entity ? entity.Id.ToString("N") : "new";
        return $"{Title}:{SelectedAction}:{suffix}";
    }

    private void PopulateSelectors(AdministrationData data)
    {
        PrimaryOptions.Clear();
        EntityOptions.Clear();
        SecondaryEntityOptions.Clear();
        IEnumerable<EntityOption> values = (Title, SelectedAction) switch
        {
            (SalesModule, _) => data.Products.Where(x => x.IsForSale)
                .Where(x => string.IsNullOrWhiteSpace(ProductSearchText)
                    || x.Name.Contains(ProductSearchText.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(x => new EntityOption(x.Id, x.Name)),
            (InventoryModule, not "Agregar producto") => data.Products
                .Select(x => new EntityOption(x.Id, x.Name)),
            (LocalUseModule, "Registrar pago" or "Asignar o cambiar silla" or "Retirar silla") => data.LocalUsePeople
                .Where(x => x.IsCurrentOn(DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime)))
                .Select(x => new EntityOption(x.Id, x.Name)),
            (ObligationsModule, "Registrar pago") => data.Obligations
                .Select(x => new EntityOption(x.Id, x.Name)),
            _ => [],
        };
        foreach (EntityOption value in values.OrderBy(x => x.Display)) EntityOptions.Add(value);

        Guid? selectedPersonId = SelectedEntityOption?.Id;
        IEnumerable<EntityOption> secondary = (Title, SelectedAction) switch
        {
            (LocalUseModule, "Añadir trabajador" or "Asignar o cambiar silla") => data.Chairs
                .Where(x => !x.AssignedPersonId.HasValue || x.AssignedPersonId == selectedPersonId)
                .Select(x => new EntityOption(x.Id, x.Name)),
            _ => [],
        };
        foreach (EntityOption value in secondary.OrderBy(x => x.Display)) SecondaryEntityOptions.Add(value);
    }

    private void ConfigureFieldPresentation()
    {
        ShowPrimary = ShowDate = true;
        ShowCommitAction = true;
        ShowActionSelector = true;
        ShowRecordActions = true;
        ShowOptionalDescription = true;
        ShowProductSearch = false;
        ProductSelectionExplanation = string.Empty;
        IsAmountReadOnly = false;
        ShowSecondary = ShowExtra = ShowEndDate = ShowAmount = ShowSecondaryAmount = ShowQuantity = false;
        UsePrimarySelector = UseSecondarySelector = false;
        PrimaryLabel = "Nombre o concepto"; SecondaryLabel = "Tipo o categoría"; ExtraLabel = "Unidad o repetición";
        DateLabel = "Fecha"; EndDateLabel = "Fecha opcional"; AmountLabel = "Valor"; SecondaryAmountLabel = "Costo real"; QuantityLabel = "Cantidad";
        SecondaryOptions.Clear();
        ExtraOptions.Clear();

        switch (Title, SelectedAction)
        {
            case (LocalUseModule, "Añadir silla"):
                PrimaryLabel = "Nombre o número de la silla"; DateLabel = "Fecha de creación"; break;
            case (LocalUseModule, "Añadir trabajador"):
                PrimaryLabel = "Nombre completo"; DateLabel = "Fecha de ingreso"; ShowEndDate = true; EndDateLabel = "Fecha de retiro (opcional)";
                ShowSecondary = true; UseSecondarySelector = true; SecondaryLabel = "Silla disponible"; break;
            case (LocalUseModule, "Registrar pago"):
                PrimaryLabel = "Trabajador"; UsePrimarySelector = true; ShowAmount = true; AmountLabel = "Valor pagado"; break;
            case (LocalUseModule, "Asignar o cambiar silla"):
                PrimaryLabel = "Trabajador"; UsePrimarySelector = true; ShowSecondary = true; UseSecondarySelector = true; SecondaryLabel = "Silla disponible"; break;
            case (LocalUseModule, "Retirar silla"):
                PrimaryLabel = "Trabajador"; UsePrimarySelector = true; break;
            case (CollaboratorsModule, "Agregar colaborador"):
                PrimaryLabel = "Nombre completo"; DateLabel = "Fecha de ingreso"; ShowEndDate = true; EndDateLabel = "Fecha de retiro (opcional)"; break;
            case (SalesModule, _):
                PrimaryLabel = "Producto destinado a venta"; UsePrimarySelector = true; ShowQuantity = ShowAmount = true;
                ShowProductSearch = true;
                ProductSelectionExplanation = "Solo aparecen productos clasificados como Alimento o bebida para venta u Otro producto para venta. Revisa la categoría en Inventario si un producto no aparece.";
                QuantityLabel = "Cantidad a vender"; AmountLabel = "Precio de venta por unidad o paquete"; IsAmountReadOnly = true; break;
            case (InventoryModule, "Agregar producto"):
                PrimaryLabel = "Nombre del producto"; ShowSecondary = ShowQuantity = ShowAmount = ShowSecondaryAmount = true;
                UseSecondarySelector = false; SecondaryLabel = "Categoría"; QuantityLabel = "Cantidad inicial";
                AmountLabel = "Costo por unidad o paquete"; SecondaryAmountLabel = "Precio de venta predeterminado (solo productos para venta)";
                DateLabel = "Fecha de ingreso";
                foreach (string x in new[] { "Alimento o bebida para venta", "Otro producto para venta", "Cortesía para clientes", "Aseo", "Insumo del local", "Otro producto del local" }) SecondaryOptions.Add(x); break;
            case (InventoryModule, "Registrar compra"):
                PrimaryLabel = "Producto existente"; UsePrimarySelector = true; ShowQuantity = ShowAmount = true; QuantityLabel = "Cantidad comprada"; AmountLabel = "Costo por unidad o paquete"; break;
            case (InventoryModule, "Registrar consumo" or "Conteo físico" or "Plan mensual"):
                PrimaryLabel = "Producto"; UsePrimarySelector = true; ShowQuantity = true; QuantityLabel = SelectedAction == "Conteo físico" ? "Cantidad física encontrada" : SelectedAction == "Plan mensual" ? "Cantidad necesaria del mes" : "Cantidad consumida"; DateLabel = SelectedAction == "Plan mensual" ? "Mes del plan" : "Fecha"; break;
            case (OtherIncomeModule, _):
                PrimaryLabel = "Nombre del ingreso"; ShowAmount = true; AmountLabel = "Valor"; break;
            case (UnexpectedModule, _):
                PrimaryLabel = "Nombre del imprevisto"; ShowAmount = true; AmountLabel = "Valor"; break;
            case (ExpensesModule, _):
                PrimaryLabel = "Nombre del gasto"; ShowAmount = true; AmountLabel = "Valor"; break;
            case (ObligationsModule, "Agregar obligación"):
                PrimaryLabel = "Nombre de la obligación"; ShowSecondary = ShowExtra = ShowAmount = true; UseSecondarySelector = false; SecondaryLabel = "Tipo"; ExtraLabel = "Recurrencia"; DateLabel = "Fecha de vencimiento"; AmountLabel = "Valor esperado";
                foreach (string x in new[] { "Servicio", "Impuesto", "Otra obligación" }) SecondaryOptions.Add(x);
                foreach (string x in new[] { "Ninguna", "Mensual", "Anual" }) ExtraOptions.Add(x); break;
            case (ObligationsModule, "Registrar pago"):
                PrimaryLabel = "Obligación"; UsePrimarySelector = true; ShowAmount = true; AmountLabel = "Valor pagado"; break;
            case (MaintenanceModule, "Programar mantenimiento"):
                PrimaryLabel = "Equipo o bien"; ShowSecondary = ShowAmount = true; SecondaryLabel = "Tipo de mantenimiento"; DateLabel = "Fecha prevista"; AmountLabel = "Costo estimado (opcional)"; break;
            case (MaintenanceModule, "Registrar realización"):
                ShowPrimary = false; ShowAmount = true; DateLabel = "Fecha realizada"; AmountLabel = "Costo real"; break;
            case (PayrollModule, "Cerrar mes"):
                ShowPrimary = false; DateLabel = "Mes a cerrar"; break;
            case (PayrollModule, "Pagar distribución"):
                ShowPrimary = false; ShowAmount = true; AmountLabel = "Valor pagado"; break;
            case (PayrollModule, "Reabrir cierre"):
                ShowPrimary = ShowDate = false; break;
            case (MonthlySummaryModule or AnnualBalanceModule, _):
                ShowPrimary = false; ShowCommitAction = ShowActionSelector = ShowRecordActions = ShowOptionalDescription = false; DateLabel = Title == AnnualBalanceModule ? "Año a consultar" : "Mes a consultar"; break;
        }
    }

    private sealed record FormPayload(
        string PrimaryText,
        string SecondaryText,
        string ExtraText,
        string DateText,
        string EndDateText,
        string AmountText,
        string SecondaryAmountText,
        string QuantityText,
        string OptionalDescriptionText = "",
        Guid? SelectedEntityId = null,
        Guid? SelectedSecondaryEntityId = null);

    private void ConfigureModule()
    {
        ActionOptions.Clear();
        IsFormVisible = true;
        (string description, string[] actions) configuration = Title switch
        {
            LocalUseModule => ("Sillas, trabajadores, asignaciones, cuotas semanales y pagos.", ["Añadir silla", "Añadir trabajador", "Registrar pago", "Asignar o cambiar silla", "Retirar silla"]),
            CollaboratorsModule => ("Participantes de los cierres mensuales; no constituye nómina laboral.", ["Agregar colaborador"]),
            SalesModule => ("Ventas de productos del local. Selecciona el producto por nombre.", ["Registrar venta"]),
            InventoryModule => ("Productos, compras, consumos, conteos y necesidades mensuales.", ["Agregar producto", "Registrar compra", "Registrar consumo", "Conteo físico", "Plan mensual"]),
            OtherIncomeModule => ("Ingresos reales diferentes de uso del local y ventas.", ["Registrar ingreso"]),
            ExpensesModule => ("Gastos del local que no provienen ya de una compra de inventario.", ["Registrar gasto"]),
            UnexpectedModule => ("Daños, reparaciones y acontecimientos no planificados.", ["Registrar imprevisto"]),
            ObligationsModule => ("Servicios, impuestos y otras obligaciones; estados calculados.", ["Agregar obligación", "Registrar pago"]),
            MaintenanceModule => ("Mantenimiento previsto y realizado de equipos o bienes.", ["Programar mantenimiento", "Registrar realización"]),
            PayrollModule => ("Cierres mensuales, participantes y distribuciones.", ["Cerrar mes", "Pagar distribución", "Reabrir cierre"]),
            MonthlySummaryModule => ("Ingresos, meta, faltante y resultados. Indica una fecha del mes a consultar.", ["Consultar"]),
            AnnualBalanceModule => ("Acumulados y pendientes. Indica una fecha del año a consultar.", ["Consultar"]),
            _ => (string.Empty, []),
        };
        Description = configuration.description;
        foreach (string action in configuration.actions)
        {
            ActionOptions.Add(action);
        }

        SelectedAction = ActionOptions.FirstOrDefault() ?? string.Empty;
        IsFormVisible = ActionOptions.Count > 0;
        ShowLocalUseSummary = Title == LocalUseModule;
        ShowCharts = Title == MonthlySummaryModule;
        ShowCollaboratorHistory = Title == CollaboratorsModule;
        OnPropertyChanged(nameof(ShowSimpleFinancialTable));
        OnPropertyChanged(nameof(ShowGeneralRecordsTable));
        ConfigureFieldPresentation();
    }

    private void LoadEntity(AuditableEntity entity)
    {
        ClearForm();
        switch (entity)
        {
            case Chair item:
                PrimaryText = item.Name;
                DateText = FormatDate(item.CreationDate);
                OptionalDescriptionText = item.Description ?? string.Empty;
                break;
            case LocalUsePerson item:
                PrimaryText = item.Name;
                DateText = FormatDate(item.EntryDate);
                EndDateText = item.ExitDate.HasValue ? FormatDate(item.ExitDate.Value) : string.Empty;
                OptionalDescriptionText = item.Description ?? string.Empty;
                break;
            case LocalUsePayment item:
                DateText = FormatDate(item.PaymentDate);
                AmountText = item.Amount.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
                OptionalDescriptionText = item.Description ?? string.Empty;
                break;
            case Collaborator item:
                PrimaryText = item.Name;
                DateText = FormatDate(item.StartDate);
                EndDateText = item.ExitDate.HasValue ? FormatDate(item.ExitDate.Value) : string.Empty;
                OptionalDescriptionText = item.Description ?? string.Empty;
                break;
            case Product item:
                PrimaryText = item.Name;
                SecondaryText = ProductCategoryName(item.Category);
                SecondaryAmountText = item.DefaultSalePrice?.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture) ?? string.Empty;
                OptionalDescriptionText = item.Description ?? string.Empty;
                break;
            case InventoryMovement item:
                DateText = FormatDate(item.Date);
                QuantityText = item.QuantityDelta.ToString("0.###", CultureInfo.CurrentCulture);
                AmountText = item.CashAmount?.ToDecimal().ToString("0.00") ?? string.Empty;
                SecondaryAmountText = item.EstimatedCost?.ToDecimal().ToString("0.00") ?? string.Empty;
                OptionalDescriptionText = item.Description ?? string.Empty;
                break;
            case MonthlyRestockPlan item:
                DateText = FormatDate(item.Month.FirstDay);
                QuantityText = item.NeededQuantity.Value.ToString("0.###", CultureInfo.CurrentCulture);
                break;
            case FinancialEntry item:
                PrimaryText = item.Concept;
                SecondaryText = item.Category.HasValue ? SpanishText.For(item.Category.Value) : string.Empty;
                DateText = FormatDate(item.Date);
                AmountText = item.Amount.ToDecimal().ToString("0.00");
                OptionalDescriptionText = item.Description ?? string.Empty;
                break;
            case Obligation item:
                PrimaryText = item.Name;
                SecondaryText = SpanishText.For(item.Type);
                ExtraText = SpanishText.For(item.Recurrence);
                DateText = FormatDate(item.DueDate);
                AmountText = item.ExpectedAmount.ToDecimal().ToString("0.00");
                OptionalDescriptionText = item.Description ?? string.Empty;
                break;
            case ObligationPayment item:
                DateText = FormatDate(item.Date);
                AmountText = item.Amount.ToDecimal().ToString("0.00");
                OptionalDescriptionText = item.Description ?? string.Empty;
                break;
            case MaintenanceRecord item:
                PrimaryText = item.Asset;
                SecondaryText = item.MaintenanceType;
                DateText = FormatDate(item.ScheduledDate);
                AmountText = item.EstimatedCost?.ToDecimal().ToString("0.00") ?? string.Empty;
                EndDateText = item.CompletedDate.HasValue ? FormatDate(item.CompletedDate.Value) : string.Empty;
                SecondaryAmountText = item.ActualCost?.ToDecimal().ToString("0.00") ?? string.Empty;
                OptionalDescriptionText = item.Description ?? string.Empty;
                break;
            case DistributionPayment item:
                DateText = FormatDate(item.Date);
                AmountText = item.Amount.ToDecimal().ToString("0.00");
                OptionalDescriptionText = item.Description ?? string.Empty;
                break;
            default:
                StatusMessage = "Este registro es histórico o calculado y no admite edición directa.";
                IsError = true;
                break;
        }
    }

    private T RequireSelected<T>() where T : AuditableEntity => SelectedRow?.Entity is T entity
        ? entity
        : throw new InvalidOperationException($"Selecciona primero un registro de tipo {typeof(T).Name}.");

    private EntityOption RequireSelectedOption() => SelectedEntityOption
        ?? throw new InvalidOperationException("Selecciona un registro válido.");

    private EntityOption RequireSecondaryOption() => SelectedSecondaryEntityOption
        ?? throw new InvalidOperationException("Selecciona una silla disponible.");

    private static Product FindProduct(AdministrationData data, string name)
    {
        Product[] matches = data.Products
            .Where(item => string.Equals(item.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length switch
        {
            0 => throw new InvalidOperationException("No existe un producto activo con ese nombre."),
            1 => matches[0],
            _ => throw new InvalidOperationException(
                "Hay más de un producto con ese nombre. Corrige los nombres duplicados antes de continuar."),
        };
    }

    private static DateOnly ParseDate(string value, string field) =>
        DateOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateOnly result)
        || DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)
            ? result
            : throw new ArgumentException($"El campo {field} debe ser una fecha válida.");

    private static bool TryParseDate(string value, out DateOnly result) =>
        DateOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out result)
        || DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);

    private static DateOnly? ParseOptionalDate(string value) => string.IsNullOrWhiteSpace(value)
        ? null
        : ParseDate(value, "fecha opcional");

    private static decimal ParseDecimal(string value, string field) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal result)
        || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result)
            ? result
            : throw new ArgumentException($"El campo {field} debe ser numérico.");

    private static Money ParseMoney(string value) => Money.FromDecimal(ParseDecimal(value, "monto"));

    private static Money? ParseOptionalMoney(string value) => string.IsNullOrWhiteSpace(value)
        ? null
        : ParseMoney(value);

    private static ProductCategory ParseProductCategory(string value) => Normalize(value) switch
    {
        "alimento o bebida para venta" => ProductCategory.FoodOrDrinkForSale,
        "otro producto para venta" or "producto para venta" or "productforsale" => ProductCategory.OtherProductForSale,
        "cortesia para clientes" or "insumo opcional" or "optionalcustomersupply" => ProductCategory.CustomerCourtesy,
        "aseo" => ProductCategory.Cleaning,
        "insumo del local" or "insumo obligatorio" or "mandatorysupply" => ProductCategory.LocalSupply,
        "otro producto del local" or "equipo" or "bien duradero" or "equipo o bien duradero" or "durableequipment" => ProductCategory.OtherLocalProduct,
        _ => throw new ArgumentException("Selecciona una categoría de inventario válida."),
    };

    private static ExpenseCategory ParseExpenseCategory(string value) => Normalize(value) switch
    {
        "insumo obligatorio" or "mandatorysupply" => ExpenseCategory.MandatorySupply,
        "insumo opcional" or "optionalsupply" => ExpenseCategory.OptionalSupply,
        "compra de mercancia" or "merchandisepurchase" => ExpenseCategory.MerchandisePurchase,
        "otro" or "otro gasto" or "other" => ExpenseCategory.Other,
        _ => throw new ArgumentException("Categoría válida: Insumo obligatorio, Insumo opcional, Compra de mercancía u Otro."),
    };

    private static ObligationType ParseObligationType(string value) => Normalize(value) switch
    {
        "servicio" or "service" => ObligationType.Service,
        "impuesto" or "tax" => ObligationType.Tax,
        "otra" or "otra obligacion" or "otherrecurring" => ObligationType.OtherRecurring,
        _ => throw new ArgumentException("Tipo válido: Servicio, Impuesto u Otra."),
    };

    private static RecurrenceFrequency ParseRecurrence(string value) => Normalize(value) switch
    {
        "" or "ninguna" or "none" => RecurrenceFrequency.None,
        "mensual" or "monthly" => RecurrenceFrequency.Monthly,
        "anual" or "annual" => RecurrenceFrequency.Annual,
        _ => throw new ArgumentException("Repetición válida: Ninguna, Mensual o Anual."),
    };

    private static string Normalize(string value) => value.Trim().ToLowerInvariant()
        .Replace("í", "i", StringComparison.Ordinal)
        .Replace("ó", "o", StringComparison.Ordinal);

    private static OperationRow Row(
        DateOnly? date,
        string principal,
        string detail,
        string quantity,
        string amount,
        string status,
        AuditableEntity? entity) => new(
            date.HasValue ? FormatDate(date.Value) : string.Empty,
            principal,
            detail,
            quantity,
            amount,
            status,
            entity);

    private static OperationRow SummaryRow(YearMonth month, string label, long amount, string currency) =>
        Row(month.FirstDay, label, month.ToString(), string.Empty, FormatMinorUnits(amount, currency),
            amount < 0 ? "Negativo" : "Calculado", null);

    private static string FormatMoney(Money? amount, string currency) => amount.HasValue
        ? FormatMinorUnits(amount.Value.MinorUnits, currency)
        : string.Empty;

    private static string FormatMinorUnits(long amount, string currency) =>
        $"{currency} {amount / 100m:N2}";

    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string ProductCategoryName(ProductCategory category) => SpanishText.For(category);

    private static string MovementTypeName(InventoryMovementType type) => type switch
    {
        InventoryMovementType.InitialStock => "Existencia inicial",
        InventoryMovementType.Purchase => "Compra",
        InventoryMovementType.Sale => "Venta",
        InventoryMovementType.InternalConsumption => "Consumo interno",
        InventoryMovementType.PhysicalCountAdjustment => "Ajuste por conteo",
        _ => type.ToString(),
    };

    private void ClearForm(bool keepMessage = false)
    {
        PrimaryText = string.Empty;
        SecondaryText = string.Empty;
        ExtraText = string.Empty;
        DateText = FormatDate(DateOnly.FromDateTime(DateTime.Today));
        EndDateText = string.Empty;
        AmountText = string.Empty;
        SecondaryAmountText = string.Empty;
        QuantityText = string.Empty;
        OptionalDescriptionText = string.Empty;
        SelectedEntityOption = null;
        SelectedSecondaryEntityOption = null;
        SelectedProductAvailability = string.Empty;
        HasRecoveredDraft = false;
        ConfirmDelete = false;
        if (!keepMessage)
        {
            StatusMessage = string.Empty;
            IsError = false;
        }
    }

    private void SetError(string message, Exception exception)
    {
        StatusMessage = message;
#if DEBUG
        StatusMessage += $"{Environment.NewLine}{exception}";
#else
        _ = exception;
#endif
        IsError = true;
    }
}
