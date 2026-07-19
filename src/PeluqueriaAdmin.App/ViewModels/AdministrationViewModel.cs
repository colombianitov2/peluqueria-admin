using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    public const string CashFlowModule = "Flujo de caja";

    [ObservableProperty]
    private string title = LocalUseModule;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string selectedAction = string.Empty;

    [ObservableProperty]
    private OperationRow? selectedRow;

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

    public ObservableCollection<string> SecondaryOptions { get; } = [];

    public ObservableCollection<string> ExtraOptions { get; } = [];

    public ObservableCollection<string> ActionOptions { get; } = [];

    public ObservableCollection<OperationRow> Rows { get; } = [];

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

            PopulateSelectors(data);

            StatusMessage = Rows.Count == 0 ? "No hay registros para mostrar." : string.Empty;
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
        LoadEntity(SelectedRow.Entity);
        suppressFormTracking = false;
        isEditing = true;
        StatusMessage = "Edición activa: los cambios válidos se guardan automáticamente.";
        IsError = false;
        _ = RestoreDraftAsync();
    }

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
            case (LocalUseModule, "Agregar persona"):
                await service.AddLocalUsePersonAsync(
                    LocalUsePerson.Create(PrimaryText, date, ParseOptionalDate(EndDateText), utcNow),
                    DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
                    completedDraftKey: completedDraftKey);
                break;
            case (LocalUseModule, "Registrar pago"):
                await service.RegisterLocalUsePaymentAsync(
                    RequireSelected<LocalUsePerson>().Id, date, ParseMoney(AmountText), completedDraftKey: completedDraftKey);
                break;
            case (CollaboratorsModule, "Agregar colaborador"):
                await service.AddAsync(Collaborator.Create(
                    PrimaryText, date, ParseOptionalDate(EndDateText), utcNow), completedDraftKey: completedDraftKey);
                break;
            case (SalesModule, "Registrar venta"):
                await AddSaleAsync(date, utcNow, completedDraftKey);
                break;
            case (InventoryModule, "Agregar producto"):
                await service.AddProductAsync(Product.Create(
                    PrimaryText, ParseProductCategory(SecondaryText), ExtraText, utcNow), completedDraftKey: completedDraftKey);
                break;
            case (InventoryModule, "Existencia inicial"):
                await AddInventoryEntryAsync(date, utcNow, InventoryMovementType.InitialStock, completedDraftKey);
                break;
            case (InventoryModule, "Registrar compra"):
                await AddInventoryEntryAsync(date, utcNow, InventoryMovementType.Purchase, completedDraftKey);
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
                await service.AddAsync(FinancialEntry.CreateIncome(date, PrimaryText, ParseMoney(AmountText), utcNow), completedDraftKey: completedDraftKey);
                break;
            case (ExpensesModule, "Registrar gasto"):
                await service.AddAsync(FinancialEntry.CreateExpense(
                    date, PrimaryText, ParseExpenseCategory(SecondaryText), ParseMoney(AmountText), utcNow), completedDraftKey: completedDraftKey);
                break;
            case (UnexpectedModule, "Registrar imprevisto"):
                await service.AddAsync(FinancialEntry.CreateUnexpectedExpense(
                    date, PrimaryText, ParseMoney(AmountText), utcNow), completedDraftKey: completedDraftKey);
                break;
            case (ObligationsModule, "Agregar obligación"):
                await service.AddObligationAsync(
                    Obligation.Create(
                        PrimaryText,
                        ParseObligationType(SecondaryText),
                        date,
                        ParseMoney(AmountText),
                        ParseRecurrence(ExtraText),
                        utcNow),
                    new YearMonth(
                        timeProvider.GetLocalNow().Year,
                        timeProvider.GetLocalNow().Month).LastDay,
                    completedDraftKey: completedDraftKey);
                break;
            case (ObligationsModule, "Registrar pago"):
                await service.AddAsync(ObligationPayment.Create(
                    RequireSelected<Obligation>().Id, date, ParseMoney(AmountText), utcNow), completedDraftKey: completedDraftKey);
                break;
            case (MaintenanceModule, "Agregar mantenimiento"):
                await service.AddAsync(MaintenanceRecord.Create(
                    PrimaryText,
                    SecondaryText,
                    date,
                    ParseOptionalMoney(AmountText),
                    ParseOptionalDate(EndDateText),
                    ParseOptionalMoney(SecondaryAmountText),
                    utcNow), completedDraftKey: completedDraftKey);
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
            case (MonthlySummaryModule or AnnualBalanceModule or CashFlowModule, "Consultar"):
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
        Product product = FindProduct(data, PrimaryText);
        decimal current = InventoryCalculator.CurrentQuantity(
            data.InventoryMovements.Where(item => item.ProductId == product.Id));
        InventoryMovement movement = InventoryMovement.Consumption(
            product.Id, date, Quantity.Positive(ParseDecimal(QuantityText, "cantidad")), current, utcNow);
        await service.AddInventoryMovementAsync(movement, completedDraftKey: completedDraftKey);
    }

    private async Task AddPhysicalCountAsync(DateOnly date, DateTime utcNow, string completedDraftKey)
    {
        AdministrationData data = await service.LoadAsync();
        Product product = FindProduct(data, PrimaryText);
        decimal current = InventoryCalculator.CurrentQuantity(
            data.InventoryMovements.Where(item => item.ProductId == product.Id));
        InventoryMovement movement = InventoryMovement.PhysicalCount(
            product.Id, date, Quantity.NonNegative(ParseDecimal(QuantityText, "cantidad física")), current, utcNow);
        await service.AddInventoryMovementAsync(movement, completedDraftKey: completedDraftKey);
    }

    private async Task AddRestockPlanAsync(DateOnly date, DateTime utcNow, string completedDraftKey)
    {
        AdministrationData data = await service.LoadAsync();
        Product product = FindProduct(data, PrimaryText);
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
            completedDraftKey: completedDraftKey);
    }

    private async Task PayDistributionAsync(DateOnly date, string completedDraftKey)
    {
        MonthlyCloseParticipant participant = RequireSelected<MonthlyCloseParticipant>();
        await service.RegisterDistributionPaymentAsync(participant.Id, date, ParseMoney(AmountText), completedDraftKey: completedDraftKey);
    }

    private async Task UpdateEntityAsync(AuditableEntity entity, string completedDraftKey)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        DateOnly date = ParseDate(DateText, "fecha");
        switch (entity)
        {
            case LocalUsePerson person:
                await service.UpdateLocalUsePersonAsync(
                    person.Id,
                    PrimaryText,
                    date,
                    ParseOptionalDate(EndDateText),
                    DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
                    completedDraftKey: completedDraftKey);
                break;
            case LocalUsePayment payment:
                AdministrationData localData = await service.LoadAsync();
                Money available = WeeklyChargeCalculator.CalculateDebt(
                    localData.WeeklyCharges.Where(item => item.PersonId == payment.PersonId),
                    localData.LocalUsePayments.Where(item => item.PersonId == payment.PersonId && item.Id != payment.Id));
                payment.Update(date, ParseMoney(AmountText), available, utcNow);
                await service.UpdateAsync(payment, completedDraftKey: completedDraftKey);
                break;
            case Collaborator collaborator:
                collaborator.Update(PrimaryText, date, ParseOptionalDate(EndDateText), utcNow);
                await service.UpdateAsync(collaborator, completedDraftKey: completedDraftKey);
                break;
            case Product product:
                await service.UpdateProductAsync(
                    product.Id, PrimaryText, ParseProductCategory(SecondaryText), ExtraText, completedDraftKey: completedDraftKey);
                break;
            case InventoryMovement movement:
                movement.Correct(
                    date,
                    ParseDecimal(QuantityText, "variación de cantidad"),
                    ParseOptionalMoney(AmountText),
                    ParseOptionalMoney(SecondaryAmountText),
                    utcNow);
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
                    utcNow);
                await service.UpdateAsync(financial, completedDraftKey: completedDraftKey);
                break;
            case Obligation obligation:
                obligation.Update(
                    PrimaryText,
                    ParseObligationType(SecondaryText),
                    date,
                    ParseMoney(AmountText),
                    ParseRecurrence(ExtraText),
                    utcNow);
                await service.UpdateAsync(obligation, completedDraftKey: completedDraftKey);
                await service.GenerateScheduledRecordsAsync(new YearMonth(
                    timeProvider.GetLocalNow().Year,
                    timeProvider.GetLocalNow().Month).LastDay);
                break;
            case ObligationPayment obligationPayment:
                obligationPayment.Update(date, ParseMoney(AmountText), utcNow);
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
                    utcNow);
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
                    utcNow);
                await service.UpdateAsync(distribution, completedDraftKey: completedDraftKey);
                break;
            default:
                throw new InvalidOperationException("Este registro es histórico o calculado y no admite edición directa.");
        }
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
        CashFlowModule => BuildCashRows(data, settings),
        _ => [],
    };

    private static IEnumerable<OperationRow> BuildLocalUseRows(AdministrationData data, SettingsDto settings)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        ChairCapacity capacity = HomeDashboardCalculator.Capacity(data, settings.TotalChairs, today);
        yield return Row(null, "Total de sillas", "Capacidad configurada", capacity.Total.ToString(CultureInfo.CurrentCulture), string.Empty, "Calculado", null);
        yield return Row(null, "Personas vigentes", "Uso del local", capacity.CurrentPeople.ToString(CultureInfo.CurrentCulture), string.Empty, "Calculado", null);
        yield return Row(
            null,
            "Sillas disponibles",
            "Capacidad actual",
            capacity.Available.ToString(CultureInfo.CurrentCulture),
            string.Empty,
            capacity.Overcapacity > 0 ? $"Sobrecupo: {capacity.Overcapacity}" : "Sin sobrecupo",
            null);

        foreach (LocalUsePerson person in data.LocalUsePeople)
        {
            Money debt = WeeklyChargeCalculator.CalculateDebt(
                data.WeeklyCharges.Where(item => item.PersonId == person.Id),
                data.LocalUsePayments.Where(item => item.PersonId == person.Id));
            yield return Row(
                person.EntryDate, person.Name, "Persona", string.Empty,
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
                null, product.Name, $"{ProductCategoryName(product.Category)} · {product.UnitOfMeasure}",
                current.ToString("0.###", CultureInfo.CurrentCulture), string.Empty, "Existencia actual", product);
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

    private IEnumerable<OperationRow> BuildCashRows(AdministrationData data, SettingsDto settings) =>
        BuildCashMovements(data)
            .Where(item => item.Date >= ParseDate(DateText, "fecha inicial")
                && item.Date <= (ParseOptionalDate(EndDateText) ?? ParseDate(DateText, "fecha inicial")))
            .OrderByDescending(item => item.Date)
            .Select(item => Row(
                item.Date, item.Concept, item.Category, string.Empty,
                FormatMinorUnits(item.SignedMinorUnits, settings.CurrencyCode),
                item.SignedMinorUnits >= 0 ? "Entrada" : "Salida", null));

    internal static MonthlySummaryInput BuildMonthlyInput(
        AdministrationData data,
        SettingsDto settings,
        YearMonth month) => AdministrationReports.BuildMonthlyInput(
            data,
            Money.FromDecimal(settings.OptionalSuppliesMonthlyBudget),
            month);

    internal static IReadOnlyList<CashMovement> BuildCashMovements(AdministrationData data)
    {
        var result = new List<CashMovement>();
        Guid[] confirmedCloseIds = data.MonthlyCloses.Where(item => item.IsConfirmed).Select(item => item.Id).ToArray();
        Guid[] validParticipantIds = data.MonthlyCloseParticipants
            .Where(item => confirmedCloseIds.Contains(item.CloseId))
            .Select(item => item.Id)
            .ToArray();
        result.AddRange(data.LocalUsePayments.Select(item =>
            new CashMovement(item.PaymentDate, LocalUseModule, "Pago por uso del local", item.Amount.MinorUnits)));
        result.AddRange(data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Sale).Select(item =>
            new CashMovement(item.Date, SalesModule, "Venta", item.CashAmount?.MinorUnits ?? 0)));
        result.AddRange(data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Purchase).Select(item =>
            new CashMovement(item.Date, "Compras", "Compra de inventario", -(item.CashAmount?.MinorUnits ?? 0))));
        result.AddRange(data.FinancialEntries.Select(item => new CashMovement(
            item.Date,
            SpanishText.For(item.Type),
            item.Concept,
            item.Type == FinancialEntryType.OtherIncome ? item.Amount.MinorUnits : -item.Amount.MinorUnits)));
        result.AddRange(data.ObligationPayments.Select(item =>
            new CashMovement(item.Date, ObligationsModule, "Pago de obligación", -item.Amount.MinorUnits)));
        result.AddRange(data.MaintenanceRecords
            .Where(item => item.CompletedDate.HasValue && item.ActualCost.HasValue)
            .Select(item => new CashMovement(
                item.CompletedDate!.Value, MaintenanceModule, item.Asset, -item.ActualCost!.Value.MinorUnits)));
        result.AddRange(data.DistributionPayments.Where(item => validParticipantIds.Contains(item.ParticipantId)).Select(item =>
            new CashMovement(item.Date, PayrollModule, "Distribución pagada", -item.Amount.MinorUnits)));
        return result;
    }

    [RelayCommand]
    private async Task DiscardDraftAsync()
    {
        string key = CurrentDraftKey();
        editAutosaveCancellation?.Cancel();
        await formDraftStore.DeleteAsync(key);
        suppressFormTracking = true;
        isEditing = false;
        ClearForm();
        suppressFormTracking = false;
        HasRecoveredDraft = false;
        StatusMessage = "El borrador se descartó. No se modificó ninguna operación registrada.";
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
            || Title is MonthlySummaryModule or AnnualBalanceModule or CashFlowModule)
        {
            return;
        }

        if (!isEditing && string.IsNullOrWhiteSpace(PrimaryText) && string.IsNullOrWhiteSpace(SecondaryText)
            && string.IsNullOrWhiteSpace(ExtraText) && string.IsNullOrWhiteSpace(EndDateText)
            && string.IsNullOrWhiteSpace(AmountText) && string.IsNullOrWhiteSpace(SecondaryAmountText)
            && string.IsNullOrWhiteSpace(QuantityText))
        {
            return;
        }

        _ = PersistDraftSafelyAsync();
        if (isEditing)
        {
            ScheduleEditAutosave();
        }
    }

    private async Task PersistDraftSafelyAsync()
    {
        await draftWriteLock.WaitAsync();
        try
        {
            string payload = JsonSerializer.Serialize(new FormPayload(
                PrimaryText, SecondaryText, ExtraText, DateText, EndDateText,
                AmountText, SecondaryAmountText, QuantityText));
            Guid? entityId = isEditing ? SelectedRow?.Entity?.Id : null;
            await formDraftStore.UpsertAsync(FormDraft.Create(
                CurrentDraftKey(), Title, SelectedAction, payload, entityId, isEditing,
                timeProvider.GetUtcNow().UtcDateTime));
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible conservar el borrador: {exception.Message}";
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
        suppressFormTracking = false;
        HasRecoveredDraft = true;
        StatusMessage = "Se recuperó un borrador sin finalizar. Puedes continuarlo o descartarlo.";
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
            StatusMessage = $"Borrador conservado. {exception.Message}";
            IsError = true;
        }
        catch (Exception exception)
        {
            SetError("No fue posible autoguardar la edición; el borrador se conservó.", exception);
        }
    }

    partial void OnPrimaryTextChanged(string value) => TrackFormChange();
    partial void OnSecondaryTextChanged(string value) => TrackFormChange();
    partial void OnExtraTextChanged(string value) => TrackFormChange();
    partial void OnAmountTextChanged(string value) => TrackFormChange();
    partial void OnSecondaryAmountTextChanged(string value) => TrackFormChange();
    partial void OnQuantityTextChanged(string value) => TrackFormChange();

    partial void OnDateTextChanged(string value)
    {
        if (TryParseDate(value, out DateOnly date)) FormDate = date.ToDateTime(TimeOnly.MinValue);
        TrackFormChange();
        if (Title is MonthlySummaryModule or AnnualBalanceModule or CashFlowModule) _ = RefreshAsync();
    }

    partial void OnEndDateTextChanged(string value)
    {
        FormEndDate = TryParseDate(value, out DateOnly date) ? date.ToDateTime(TimeOnly.MinValue) : null;
        TrackFormChange();
        if (Title == CashFlowModule) _ = RefreshAsync();
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
        IEnumerable<string> values = (Title, SelectedAction) switch
        {
            (SalesModule, _) or (InventoryModule, not "Agregar producto") => data.Products.Select(x => x.Name),
            (LocalUseModule, "Registrar pago") => data.LocalUsePeople.Select(x => x.Name),
            (ObligationsModule, "Registrar pago") => data.Obligations.Select(x => x.Name),
            _ => [],
        };
        foreach (string value in values.OrderBy(x => x)) PrimaryOptions.Add(value);
    }

    private void ConfigureFieldPresentation()
    {
        ShowPrimary = ShowDate = true;
        ShowCommitAction = true;
        ShowActionSelector = true;
        ShowRecordActions = true;
        ShowSecondary = ShowExtra = ShowEndDate = ShowAmount = ShowSecondaryAmount = ShowQuantity = false;
        UsePrimarySelector = UseSecondarySelector = false;
        PrimaryLabel = "Nombre o concepto"; SecondaryLabel = "Tipo o categoría"; ExtraLabel = "Unidad o repetición";
        DateLabel = "Fecha"; EndDateLabel = "Fecha opcional"; AmountLabel = "Valor"; SecondaryAmountLabel = "Costo real"; QuantityLabel = "Cantidad";
        SecondaryOptions.Clear();
        ExtraOptions.Clear();

        switch (Title, SelectedAction)
        {
            case (LocalUseModule, "Agregar persona"):
            case (CollaboratorsModule, "Agregar colaborador"):
                PrimaryLabel = "Nombre completo"; DateLabel = "Fecha de ingreso"; ShowEndDate = true; EndDateLabel = "Fecha de retiro (opcional)"; break;
            case (LocalUseModule, "Registrar pago"):
                PrimaryLabel = "Persona"; UsePrimarySelector = true; ShowAmount = true; AmountLabel = "Valor pagado"; break;
            case (SalesModule, _):
                PrimaryLabel = "Producto"; UsePrimarySelector = true; ShowQuantity = ShowAmount = true; QuantityLabel = "Cantidad vendida"; AmountLabel = "Precio unitario"; break;
            case (InventoryModule, "Agregar producto"):
                PrimaryLabel = "Nombre del producto"; ShowSecondary = ShowExtra = true; UseSecondarySelector = true; SecondaryLabel = "Categoría"; ExtraLabel = "Unidad de medida";
                foreach (string x in new[] { "Producto para venta", "Insumo obligatorio", "Insumo opcional", "Equipo o bien duradero" }) SecondaryOptions.Add(x); break;
            case (InventoryModule, "Existencia inicial" or "Registrar compra"):
                PrimaryLabel = "Producto"; UsePrimarySelector = true; ShowQuantity = ShowAmount = true; QuantityLabel = "Cantidad"; AmountLabel = "Costo total"; break;
            case (InventoryModule, "Registrar consumo" or "Conteo físico" or "Plan mensual"):
                PrimaryLabel = "Producto"; UsePrimarySelector = true; ShowQuantity = true; QuantityLabel = SelectedAction == "Conteo físico" ? "Cantidad física encontrada" : SelectedAction == "Plan mensual" ? "Cantidad necesaria del mes" : "Cantidad consumida"; DateLabel = SelectedAction == "Plan mensual" ? "Mes del plan" : "Fecha"; break;
            case (OtherIncomeModule or UnexpectedModule, _):
                PrimaryLabel = "Concepto"; ShowAmount = true; AmountLabel = "Valor"; break;
            case (ExpensesModule, _):
                PrimaryLabel = "Concepto"; ShowSecondary = ShowAmount = true; UseSecondarySelector = true; SecondaryLabel = "Categoría";
                foreach (string x in new[] { "Insumo obligatorio", "Insumo opcional", "Compra de mercancía", "Otro gasto" }) SecondaryOptions.Add(x); break;
            case (ObligationsModule, "Agregar obligación"):
                PrimaryLabel = "Nombre de la obligación"; ShowSecondary = ShowExtra = ShowAmount = true; UseSecondarySelector = true; SecondaryLabel = "Tipo"; ExtraLabel = "Recurrencia"; DateLabel = "Fecha de vencimiento"; AmountLabel = "Valor esperado";
                foreach (string x in new[] { "Servicio", "Impuesto", "Otra obligación" }) SecondaryOptions.Add(x);
                foreach (string x in new[] { "Ninguna", "Mensual", "Anual" }) ExtraOptions.Add(x); break;
            case (ObligationsModule, "Registrar pago"):
                PrimaryLabel = "Obligación"; UsePrimarySelector = true; ShowAmount = true; AmountLabel = "Valor pagado"; break;
            case (MaintenanceModule, _):
                PrimaryLabel = "Equipo o bien"; ShowSecondary = ShowAmount = ShowEndDate = ShowSecondaryAmount = true; SecondaryLabel = "Tipo de mantenimiento"; DateLabel = "Fecha programada"; AmountLabel = "Costo estimado (opcional)"; EndDateLabel = "Fecha realizada (opcional)"; SecondaryAmountLabel = "Costo real (opcional)"; break;
            case (PayrollModule, "Cerrar mes"):
                ShowPrimary = false; DateLabel = "Mes a cerrar"; break;
            case (PayrollModule, "Pagar distribución"):
                ShowPrimary = false; ShowAmount = true; AmountLabel = "Valor pagado"; break;
            case (PayrollModule, "Reabrir cierre"):
                ShowPrimary = ShowDate = false; break;
            case (MonthlySummaryModule or AnnualBalanceModule, _):
                ShowPrimary = false; ShowCommitAction = ShowActionSelector = ShowRecordActions = false; DateLabel = Title == AnnualBalanceModule ? "Año a consultar" : "Mes a consultar"; break;
            case (CashFlowModule, _):
                ShowPrimary = false; ShowCommitAction = ShowActionSelector = ShowRecordActions = false; DateLabel = "Fecha inicial"; ShowEndDate = true; EndDateLabel = "Fecha final"; break;
        }

        if (Title == InventoryModule && SelectedAction == "Agregar producto")
        {
            foreach (string x in new[] { "unidad", "mililitro", "litro", "gramo", "kilogramo" }) ExtraOptions.Add(x);
        }
    }

    private sealed record FormPayload(string PrimaryText, string SecondaryText, string ExtraText, string DateText, string EndDateText, string AmountText, string SecondaryAmountText, string QuantityText);

    private void ConfigureModule()
    {
        ActionOptions.Clear();
        IsFormVisible = true;
        (string description, string[] actions) configuration = Title switch
        {
            LocalUseModule => ("Personas, cuotas cada siete días, pagos y deuda actual.", ["Agregar persona", "Registrar pago"]),
            CollaboratorsModule => ("Participantes de los cierres mensuales; no constituye nómina laboral.", ["Agregar colaborador"]),
            SalesModule => ("Ventas de productos del local. Selecciona el producto por nombre.", ["Registrar venta"]),
            InventoryModule => ("Productos, movimientos, conteos y necesidades mensuales.", ["Agregar producto", "Existencia inicial", "Registrar compra", "Registrar consumo", "Conteo físico", "Plan mensual"]),
            OtherIncomeModule => ("Ingresos reales diferentes de uso del local y ventas.", ["Registrar ingreso"]),
            ExpensesModule => ("Gastos del local que no provienen ya de una compra de inventario.", ["Registrar gasto"]),
            UnexpectedModule => ("Daños, reparaciones y acontecimientos no planificados.", ["Registrar imprevisto"]),
            ObligationsModule => ("Servicios, impuestos y otras obligaciones; estados calculados.", ["Agregar obligación", "Registrar pago"]),
            MaintenanceModule => ("Mantenimiento previsto y realizado de equipos o bienes.", ["Agregar mantenimiento"]),
            PayrollModule => ("Cierres mensuales, participantes y distribuciones.", ["Cerrar mes", "Pagar distribución", "Reabrir cierre"]),
            MonthlySummaryModule => ("Ingresos, meta, faltante y resultados. Indica una fecha del mes a consultar.", ["Consultar"]),
            AnnualBalanceModule => ("Acumulados y pendientes. Indica una fecha del año a consultar.", ["Consultar"]),
            CashFlowModule => ("Movimientos reales sin duplicados. Usa Fecha y Retiro/realización como rango.", ["Consultar"]),
            _ => (string.Empty, []),
        };
        Description = configuration.description;
        foreach (string action in configuration.actions)
        {
            ActionOptions.Add(action);
        }

        SelectedAction = ActionOptions.FirstOrDefault() ?? string.Empty;
        IsFormVisible = ActionOptions.Count > 0;
        ConfigureFieldPresentation();
    }

    private void LoadEntity(AuditableEntity entity)
    {
        ClearForm();
        switch (entity)
        {
            case LocalUsePerson item:
                PrimaryText = item.Name;
                DateText = FormatDate(item.EntryDate);
                EndDateText = item.ExitDate.HasValue ? FormatDate(item.ExitDate.Value) : string.Empty;
                break;
            case LocalUsePayment item:
                DateText = FormatDate(item.PaymentDate);
                AmountText = item.Amount.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
                break;
            case Collaborator item:
                PrimaryText = item.Name;
                DateText = FormatDate(item.StartDate);
                EndDateText = item.ExitDate.HasValue ? FormatDate(item.ExitDate.Value) : string.Empty;
                break;
            case Product item:
                PrimaryText = item.Name;
                SecondaryText = ProductCategoryName(item.Category);
                ExtraText = item.UnitOfMeasure;
                break;
            case InventoryMovement item:
                DateText = FormatDate(item.Date);
                QuantityText = item.QuantityDelta.ToString("0.###", CultureInfo.CurrentCulture);
                AmountText = item.CashAmount?.ToDecimal().ToString("0.00") ?? string.Empty;
                SecondaryAmountText = item.EstimatedCost?.ToDecimal().ToString("0.00") ?? string.Empty;
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
                break;
            case Obligation item:
                PrimaryText = item.Name;
                SecondaryText = SpanishText.For(item.Type);
                ExtraText = SpanishText.For(item.Recurrence);
                DateText = FormatDate(item.DueDate);
                AmountText = item.ExpectedAmount.ToDecimal().ToString("0.00");
                break;
            case ObligationPayment item:
                DateText = FormatDate(item.Date);
                AmountText = item.Amount.ToDecimal().ToString("0.00");
                break;
            case MaintenanceRecord item:
                PrimaryText = item.Asset;
                SecondaryText = item.MaintenanceType;
                DateText = FormatDate(item.ScheduledDate);
                AmountText = item.EstimatedCost?.ToDecimal().ToString("0.00") ?? string.Empty;
                EndDateText = item.CompletedDate.HasValue ? FormatDate(item.CompletedDate.Value) : string.Empty;
                SecondaryAmountText = item.ActualCost?.ToDecimal().ToString("0.00") ?? string.Empty;
                break;
            case DistributionPayment item:
                DateText = FormatDate(item.Date);
                AmountText = item.Amount.ToDecimal().ToString("0.00");
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
        "producto para venta" or "productforsale" => ProductCategory.ProductForSale,
        "insumo obligatorio" or "mandatorysupply" => ProductCategory.MandatorySupply,
        "insumo opcional" or "optionalcustomersupply" => ProductCategory.OptionalCustomerSupply,
        "equipo" or "bien duradero" or "equipo o bien duradero" or "durableequipment" => ProductCategory.DurableEquipment,
        _ => throw new ArgumentException("Categoría válida: Producto para venta, Insumo obligatorio, Insumo opcional o Equipo."),
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
