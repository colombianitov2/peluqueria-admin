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

public sealed partial class AdministrationViewModel(
    AdministrationService service,
    GetSettingsUseCase getSettings,
    TimeProvider timeProvider) : ObservableObject
{
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

    public ObservableCollection<string> ActionOptions { get; } = [];

    public ObservableCollection<OperationRow> Rows { get; } = [];

    public async Task SelectModuleAsync(string module)
    {
        Title = module;
        ConfigureModule();
        ClearForm();
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            AdministrationData data = await service.LoadAsync();
            SettingsDto settings = await getSettings.ExecuteAsync();
            Rows.Clear();
            foreach (OperationRow row in BuildRows(data, settings))
            {
                Rows.Add(row);
            }

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
            await ExecuteSelectedActionAsync();
            StatusMessage = "La operación se guardó correctamente.";
            IsError = false;
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

        LoadEntity(SelectedRow.Entity);
        StatusMessage = "Modifica los campos y usa “Guardar edición”.";
        IsError = false;
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

        if (!ConfirmDelete)
        {
            StatusMessage = "Marca la confirmación antes de eliminar.";
            IsError = true;
            return;
        }

        IsBusy = true;
        try
        {
            await UpdateEntityAsync(entity);
            StatusMessage = "El registro se editó correctamente.";
            IsError = false;
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

    private async Task ExecuteSelectedActionAsync()
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        DateOnly date = ParseDate(DateText, "fecha");

        switch (Title, SelectedAction)
        {
            case (LocalUseModule, "Agregar persona"):
                await service.AddAsync(LocalUsePerson.Create(
                    PrimaryText, date, ParseOptionalDate(EndDateText), utcNow));
                break;
            case (LocalUseModule, "Registrar pago"):
                await service.RegisterLocalUsePaymentAsync(
                    RequireSelected<LocalUsePerson>().Id, date, ParseMoney(AmountText), default);
                break;
            case (CollaboratorsModule, "Agregar colaborador"):
                await service.AddAsync(Collaborator.Create(
                    PrimaryText, date, ParseOptionalDate(EndDateText), utcNow));
                break;
            case (SalesModule, "Registrar venta"):
                await AddSaleAsync(date, utcNow);
                break;
            case (InventoryModule, "Agregar producto"):
                await service.AddAsync(Product.Create(
                    PrimaryText, ParseProductCategory(SecondaryText), ExtraText, utcNow));
                break;
            case (InventoryModule, "Existencia inicial"):
                await AddInventoryEntryAsync(date, utcNow, InventoryMovementType.InitialStock);
                break;
            case (InventoryModule, "Registrar compra"):
                await AddInventoryEntryAsync(date, utcNow, InventoryMovementType.Purchase);
                break;
            case (InventoryModule, "Registrar consumo"):
                await AddConsumptionAsync(date, utcNow);
                break;
            case (InventoryModule, "Conteo físico"):
                await AddPhysicalCountAsync(date, utcNow);
                break;
            case (InventoryModule, "Plan mensual"):
                await AddRestockPlanAsync(date, utcNow);
                break;
            case (OtherIncomeModule, "Registrar ingreso"):
                await service.AddAsync(FinancialEntry.CreateIncome(date, PrimaryText, ParseMoney(AmountText), utcNow));
                break;
            case (ExpensesModule, "Registrar gasto"):
                await service.AddAsync(FinancialEntry.CreateExpense(
                    date, PrimaryText, ParseExpenseCategory(SecondaryText), ParseMoney(AmountText), utcNow));
                break;
            case (UnexpectedModule, "Registrar imprevisto"):
                await service.AddAsync(FinancialEntry.CreateUnexpectedExpense(
                    date, PrimaryText, ParseMoney(AmountText), utcNow));
                break;
            case (ObligationsModule, "Agregar obligación"):
                await service.AddAsync(Obligation.Create(
                    PrimaryText,
                    ParseObligationType(SecondaryText),
                    date,
                    ParseMoney(AmountText),
                    ParseRecurrence(ExtraText),
                    utcNow));
                break;
            case (ObligationsModule, "Registrar pago"):
                await service.AddAsync(ObligationPayment.Create(
                    RequireSelected<Obligation>().Id, date, ParseMoney(AmountText), utcNow));
                break;
            case (MaintenanceModule, "Agregar mantenimiento"):
                await service.AddAsync(MaintenanceRecord.Create(
                    PrimaryText,
                    SecondaryText,
                    date,
                    ParseOptionalMoney(AmountText),
                    ParseOptionalDate(EndDateText),
                    ParseOptionalMoney(SecondaryAmountText),
                    utcNow));
                break;
            case (PayrollModule, "Cerrar mes"):
                await CloseMonthAsync(date);
                break;
            case (PayrollModule, "Pagar distribución"):
                await PayDistributionAsync(date, utcNow);
                break;
            case (PayrollModule, "Reabrir cierre"):
                MonthlyClose close = RequireSelected<MonthlyClose>();
                close.Reopen(utcNow);
                await service.UpdateAsync(close);
                break;
            case (MonthlySummaryModule or AnnualBalanceModule or CashFlowModule, "Consultar"):
                break;
            default:
                throw new InvalidOperationException("La operación seleccionada no está disponible en este módulo.");
        }
    }

    private async Task AddSaleAsync(DateOnly date, DateTime utcNow)
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
        await service.AddInventoryMovementAsync(sale);
    }

    private async Task AddInventoryEntryAsync(
        DateOnly date,
        DateTime utcNow,
        InventoryMovementType type)
    {
        AdministrationData data = await service.LoadAsync();
        Product product = FindProduct(data, PrimaryText);
        Quantity quantity = Quantity.Positive(ParseDecimal(QuantityText, "cantidad"));
        Money cost = ParseMoney(AmountText);
        InventoryMovement movement = type == InventoryMovementType.InitialStock
            ? InventoryMovement.Initial(product.Id, date, quantity, cost, utcNow)
            : InventoryMovement.Purchase(product.Id, date, quantity, cost, utcNow);
        await service.AddInventoryMovementAsync(movement);
    }

    private async Task AddConsumptionAsync(DateOnly date, DateTime utcNow)
    {
        AdministrationData data = await service.LoadAsync();
        Product product = FindProduct(data, PrimaryText);
        decimal current = InventoryCalculator.CurrentQuantity(
            data.InventoryMovements.Where(item => item.ProductId == product.Id));
        InventoryMovement movement = InventoryMovement.Consumption(
            product.Id, date, Quantity.Positive(ParseDecimal(QuantityText, "cantidad")), current, utcNow);
        await service.AddInventoryMovementAsync(movement);
    }

    private async Task AddPhysicalCountAsync(DateOnly date, DateTime utcNow)
    {
        AdministrationData data = await service.LoadAsync();
        Product product = FindProduct(data, PrimaryText);
        decimal current = InventoryCalculator.CurrentQuantity(
            data.InventoryMovements.Where(item => item.ProductId == product.Id));
        InventoryMovement movement = InventoryMovement.PhysicalCount(
            product.Id, date, Quantity.NonNegative(ParseDecimal(QuantityText, "cantidad física")), current, utcNow);
        await service.AddInventoryMovementAsync(movement);
    }

    private async Task AddRestockPlanAsync(DateOnly date, DateTime utcNow)
    {
        AdministrationData data = await service.LoadAsync();
        Product product = FindProduct(data, PrimaryText);
        await service.AddAsync(MonthlyRestockPlan.Create(
            product.Id,
            YearMonth.From(date),
            Quantity.NonNegative(ParseDecimal(QuantityText, "necesidad mensual")),
            utcNow));
    }

    private async Task CloseMonthAsync(DateOnly date)
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
            participantIds);
    }

    private async Task PayDistributionAsync(DateOnly date, DateTime utcNow)
    {
        MonthlyCloseParticipant participant = RequireSelected<MonthlyCloseParticipant>();
        AdministrationData data = await service.LoadAsync();
        long paid = data.DistributionPayments
            .Where(item => item.ParticipantId == participant.Id)
            .Sum(item => item.Amount.MinorUnits);
        Money pending = Money.FromMinorUnits(participant.Amount.MinorUnits - paid);
        await service.AddAsync(DistributionPayment.Create(
            participant.Id, date, ParseMoney(AmountText), pending, utcNow));
    }

    private async Task UpdateEntityAsync(AuditableEntity entity)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        DateOnly date = ParseDate(DateText, "fecha");
        switch (entity)
        {
            case LocalUsePerson person:
                person.Update(PrimaryText, date, ParseOptionalDate(EndDateText), utcNow);
                await service.UpdateAsync(person);
                break;
            case LocalUsePayment payment:
                AdministrationData localData = await service.LoadAsync();
                Money available = WeeklyChargeCalculator.CalculateDebt(
                    localData.WeeklyCharges.Where(item => item.PersonId == payment.PersonId),
                    localData.LocalUsePayments.Where(item => item.PersonId == payment.PersonId && item.Id != payment.Id));
                payment.Update(date, ParseMoney(AmountText), available, utcNow);
                await service.UpdateAsync(payment);
                break;
            case Collaborator collaborator:
                collaborator.Update(PrimaryText, date, ParseOptionalDate(EndDateText), utcNow);
                await service.UpdateAsync(collaborator);
                break;
            case Product product:
                product.Update(PrimaryText, ParseProductCategory(SecondaryText), ExtraText, utcNow);
                await service.UpdateAsync(product);
                break;
            case InventoryMovement movement:
                movement.Correct(
                    date,
                    ParseDecimal(QuantityText, "variación de cantidad"),
                    ParseOptionalMoney(AmountText),
                    ParseOptionalMoney(SecondaryAmountText),
                    utcNow);
                await service.UpdateInventoryMovementAsync(movement);
                break;
            case MonthlyRestockPlan plan:
                plan.Update(
                    YearMonth.From(date),
                    Quantity.NonNegative(ParseDecimal(QuantityText, "necesidad mensual")),
                    utcNow);
                await service.UpdateAsync(plan);
                break;
            case FinancialEntry financial:
                financial.Update(
                    date,
                    PrimaryText,
                    financial.Type == FinancialEntryType.Expense ? ParseExpenseCategory(SecondaryText) : null,
                    ParseMoney(AmountText),
                    utcNow);
                await service.UpdateAsync(financial);
                break;
            case Obligation obligation:
                obligation.Update(
                    PrimaryText,
                    ParseObligationType(SecondaryText),
                    date,
                    ParseMoney(AmountText),
                    ParseRecurrence(ExtraText),
                    utcNow);
                await service.UpdateAsync(obligation);
                break;
            case ObligationPayment obligationPayment:
                obligationPayment.Update(date, ParseMoney(AmountText), utcNow);
                await service.UpdateAsync(obligationPayment);
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
                await service.UpdateAsync(maintenance);
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
                await service.UpdateAsync(distribution);
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
            item.Date, item.Concept, item.Category?.ToString() ?? type.ToString(), string.Empty,
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
                item.DueDate, item.Name, $"{item.Type} · {item.Recurrence}", string.Empty,
                item.GoalAmount(payments).ToDecimal().ToString("0.00"), item.Status(payments, today).ToString(), item);
        }

        foreach (ObligationPayment payment in data.ObligationPayments)
        {
            string name = data.Obligations.Single(item => item.Id == payment.ObligationId).Name;
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
            long paid = data.DistributionPayments.Where(item => item.ParticipantId == participant.Id).Sum(item => item.Amount.MinorUnits);
            yield return Row(
                null, name, "Participación mensual", string.Empty,
                FormatMinorUnits(participant.Amount.MinorUnits, settings.CurrencyCode),
                paid >= participant.Amount.MinorUnits ? "Pagado" : $"Pendiente {FormatMinorUnits(participant.Amount.MinorUnits - paid, settings.CurrencyCode)}",
                participant);
        }

        foreach (DistributionPayment payment in data.DistributionPayments)
        {
            yield return Row(
                payment.Date, "Pago a colaborador", "Distribución", string.Empty,
                FormatMoney(payment.Amount, settings.CurrencyCode), "Registrado", payment);
        }
    }

    private IEnumerable<OperationRow> BuildMonthlySummaryRows(AdministrationData data, SettingsDto settings)
    {
        YearMonth month = YearMonth.From(ParseDate(DateText, "mes a consultar"));
        MonthlySummaryResult result = MonthlySummaryCalculator.Calculate(
            BuildMonthlyInput(data, settings, month),
            Percentage.FromPercent(settings.CollaboratorProfitPercent));
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
        MonthlySummaryResult[] months = Enumerable.Range(1, 12)
            .Select(month => MonthlySummaryCalculator.Calculate(
                BuildMonthlyInput(data, settings, new YearMonth(year, month)),
                Percentage.FromPercent(settings.CollaboratorProfitPercent)))
            .ToArray();
        long distributions = data.DistributionPayments
            .Where(item => item.Date.Year == year)
            .Sum(item => item.Amount.MinorUnits);
        long pending = data.Obligations
            .Where(item => item.DueDate.Year == year)
            .Sum(item => Math.Max(
                0,
                item.ExpectedAmount.MinorUnits - data.ObligationPayments
                    .Where(payment => payment.ObligationId == item.Id)
                    .Sum(payment => payment.Amount.MinorUnits)));
        AnnualBalanceResult annual = AnnualBalanceCalculator.Calculate(months, distributions, pending);
        YearMonth january = new(year, 1);
        return
        [
            SummaryRow(january, "Ingresos acumulados", annual.IncomeMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Meta y gastos acumulados", annual.ExpenseMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Distribuciones pagadas", annual.DistributionMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Resultado retenido", annual.RetainedMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Pendientes", annual.PendingMinorUnits, settings.CurrencyCode),
            SummaryRow(january, "Faltante anual", annual.MissingMinorUnits, settings.CurrencyCode),
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
        YearMonth month)
    {
        bool InMonth(DateOnly date) => YearMonth.From(date) == month;
        long localUse = data.LocalUsePayments.Where(item => InMonth(item.PaymentDate)).Sum(item => item.Amount.MinorUnits);
        InventoryMovement[] purchases = data.InventoryMovements
            .Where(item => item.Type == InventoryMovementType.Purchase && InMonth(item.Date))
            .ToArray();
        long PurchaseFor(ProductCategory category) => purchases
            .Where(item => data.Products.Any(product => product.Id == item.ProductId && product.Category == category))
            .Sum(item => item.CashAmount?.MinorUnits ?? 0);
        long mandatoryInventory = PurchaseFor(ProductCategory.MandatorySupply) + PurchaseFor(ProductCategory.DurableEquipment);
        long optionalInventory = PurchaseFor(ProductCategory.OptionalCustomerSupply);
        long merchandise = PurchaseFor(ProductCategory.ProductForSale);
        long mandatoryExpenses = data.FinancialEntries
            .Where(item => item.Type == FinancialEntryType.Expense
                && item.Category != ExpenseCategory.OptionalSupply
                && InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits) + mandatoryInventory;
        long optionalExpenses = data.FinancialEntries
            .Where(item => item.Type == FinancialEntryType.Expense
                && item.Category == ExpenseCategory.OptionalSupply
                && InMonth(item.Date))
            .Sum(item => item.Amount.MinorUnits) + optionalInventory;
        long planCost = data.RestockPlans.Where(item => item.Month == month).Sum(plan =>
        {
            InventoryMovement[] productMovements = data.InventoryMovements
                .Where(item => item.ProductId == plan.ProductId && item.Date <= month.LastDay)
                .ToArray();
            decimal suggestion = plan.SuggestedPurchase(InventoryCalculator.CurrentQuantity(productMovements));
            return checked((long)decimal.Round(
                InventoryCalculator.AverageUnitCost(productMovements).MinorUnits * suggestion,
                0,
                MidpointRounding.AwayFromZero));
        });

        return new MonthlySummaryInput(
            localUse,
            data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Sale && InMonth(item.Date))
                .Sum(item => item.CashAmount?.MinorUnits ?? 0),
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.OtherIncome && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits),
            data.Obligations.Where(item => InMonth(item.DueDate))
                .Sum(item => item.GoalAmount(data.ObligationPayments).MinorUnits),
            merchandise,
            mandatoryExpenses,
            optionalExpenses,
            Money.FromDecimal(settings.OptionalSuppliesMonthlyBudget).MinorUnits,
            data.FinancialEntries.Where(item => item.Type == FinancialEntryType.UnexpectedExpense && InMonth(item.Date))
                .Sum(item => item.Amount.MinorUnits),
            data.MaintenanceRecords.Sum(item => item.GoalAmountFor(month).MinorUnits),
            planCost);
    }

    internal static IReadOnlyList<CashMovement> BuildCashMovements(AdministrationData data)
    {
        var result = new List<CashMovement>();
        result.AddRange(data.LocalUsePayments.Select(item =>
            new CashMovement(item.PaymentDate, LocalUseModule, "Pago por uso del local", item.Amount.MinorUnits)));
        result.AddRange(data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Sale).Select(item =>
            new CashMovement(item.Date, SalesModule, "Venta", item.CashAmount?.MinorUnits ?? 0)));
        result.AddRange(data.InventoryMovements.Where(item => item.Type == InventoryMovementType.Purchase).Select(item =>
            new CashMovement(item.Date, "Compras", "Compra de inventario", -(item.CashAmount?.MinorUnits ?? 0))));
        result.AddRange(data.FinancialEntries.Select(item => new CashMovement(
            item.Date,
            item.Type.ToString(),
            item.Concept,
            item.Type == FinancialEntryType.OtherIncome ? item.Amount.MinorUnits : -item.Amount.MinorUnits)));
        result.AddRange(data.ObligationPayments.Select(item =>
            new CashMovement(item.Date, ObligationsModule, "Pago de obligación", -item.Amount.MinorUnits)));
        result.AddRange(data.MaintenanceRecords
            .Where(item => item.CompletedDate.HasValue && item.ActualCost.HasValue)
            .Select(item => new CashMovement(
                item.CompletedDate!.Value, MaintenanceModule, item.Asset, -item.ActualCost!.Value.MinorUnits)));
        result.AddRange(data.DistributionPayments.Select(item =>
            new CashMovement(item.Date, PayrollModule, "Distribución pagada", -item.Amount.MinorUnits)));
        return result;
    }

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
                SecondaryText = item.Category?.ToString() ?? string.Empty;
                DateText = FormatDate(item.Date);
                AmountText = item.Amount.ToDecimal().ToString("0.00");
                break;
            case Obligation item:
                PrimaryText = item.Name;
                SecondaryText = item.Type.ToString();
                ExtraText = item.Recurrence.ToString();
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

    private static Product FindProduct(AdministrationData data, string name) => data.Products
        .SingleOrDefault(item => string.Equals(item.Name, name.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("No existe un producto activo con ese nombre.");

    private static DateOnly ParseDate(string value, string field) =>
        DateOnly.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out DateOnly result)
        || DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)
            ? result
            : throw new ArgumentException($"El campo {field} debe ser una fecha válida.");

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
        "equipo" or "bien duradero" or "durableequipment" => ProductCategory.DurableEquipment,
        _ => throw new ArgumentException("Categoría válida: Producto para venta, Insumo obligatorio, Insumo opcional o Equipo."),
    };

    private static ExpenseCategory ParseExpenseCategory(string value) => Normalize(value) switch
    {
        "insumo obligatorio" or "mandatorysupply" => ExpenseCategory.MandatorySupply,
        "insumo opcional" or "optionalsupply" => ExpenseCategory.OptionalSupply,
        "compra de mercancia" or "merchandisepurchase" => ExpenseCategory.MerchandisePurchase,
        "otro" or "other" => ExpenseCategory.Other,
        _ => throw new ArgumentException("Categoría válida: Insumo obligatorio, Insumo opcional, Compra de mercancía u Otro."),
    };

    private static ObligationType ParseObligationType(string value) => Normalize(value) switch
    {
        "servicio" or "service" => ObligationType.Service,
        "impuesto" or "tax" => ObligationType.Tax,
        "otra" or "otherrecurring" => ObligationType.OtherRecurring,
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

    private static string ProductCategoryName(ProductCategory category) => category switch
    {
        ProductCategory.ProductForSale => "Producto para venta",
        ProductCategory.MandatorySupply => "Insumo obligatorio",
        ProductCategory.OptionalCustomerSupply => "Insumo opcional",
        ProductCategory.DurableEquipment => "Equipo",
        _ => category.ToString(),
    };

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
