using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Activity;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.DataManagement;
using PeluqueriaAdmin.Application.Drafts;
using PeluqueriaAdmin.Application.Exporting;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Application.Updates;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Drafts;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class SettingsViewModel(
    GetSettingsUseCase getSettings,
    SaveSettingsUseCase saveSettings,
    IDataManagementService dataManagement,
    IExcelExportService excelExport,
    IUpdateService updateService,
    IFormDraftStore formDraftStore,
    TimeProvider timeProvider,
    AdministrationService administrationService,
    IUserDesktopPath userDesktopPath) : ObservableObject
{
    private const string SettingsDraftKey = "Ajustes:Edicion:settings";
    private string? lastExcelPath;
    private readonly SemaphoreSlim autosaveLock = new(1, 1);
    private CancellationTokenSource? autosaveCancellation;
    private CancellationTokenSource? unofficialEditCancellation;
    private bool trackingEnabled;
    private bool loadingUnofficialExpense;
    private bool subscribedToFinancialChanges;
    [ObservableProperty]
    private string weeklyUsageFee = string.Empty;

    [ObservableProperty]
    private string collaboratorProfitPercent = string.Empty;

    [ObservableProperty]
    private string exportDirectory = string.Empty;

    [ObservableProperty]
    private string unofficialExpenseName = string.Empty;

    [ObservableProperty]
    private string unofficialExpenseAmount = string.Empty;

    [ObservableProperty]
    private string unofficialExpenseEffectiveFrom = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [ObservableProperty]
    private string unofficialExpenseDescription = string.Empty;

    [ObservableProperty]
    private OperationRow? selectedUnofficialExpense;

    [ObservableProperty]
    private string selectedUnofficialPeriod = "Hoy";

    [ObservableProperty]
    private DateTime? unofficialCustomFrom = DateTime.Today;

    [ObservableProperty]
    private DateTime? unofficialCustomThrough = DateTime.Today;

    [ObservableProperty]
    private bool showUnofficialCustomPeriod;

    [ObservableProperty]
    private bool confirmUnofficialExpenseDelete;

    [ObservableProperty]
    private string restorePath = string.Empty;

    [ObservableProperty]
    private bool updateReady;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private bool isError;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private DateTime? selectedFinancialMonth = timeProvider.GetLocalNow().DateTime.Date;

    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    public ObservableCollection<string> ActivityPeriodOptions { get; } =
        ["Hoy", "Esta semana", "Este mes", "Últimos 3 meses", "Últimos 6 meses", "Este año", "Rango personalizado"];

    public ObservableCollection<OperationRow> UnofficialExpenses { get; } = [];

    public ObservableCollection<OperationRow> UnofficialExpenseActivity { get; } = [];

    public ObservableCollection<FinancialSummaryRow> FinancialSummaryRows { get; } = [];

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            SettingsDto settings = await getSettings.ExecuteAsync(cancellationToken);
            Apply(settings);
            FormDraft? draft = await formDraftStore.FindAsync(SettingsDraftKey, cancellationToken);
            if (draft is not null)
            {
                SettingsDraft? values = JsonSerializer.Deserialize<SettingsDraft>(draft.PayloadJson);
                if (values is not null)
                {
                    ApplyDraft(values);
                    StatusMessage = string.Empty;
                }
            }
            trackingEnabled = true;
            if (string.IsNullOrWhiteSpace(UnofficialExpenseEffectiveFrom))
            {
                UnofficialExpenseEffectiveFrom = LocalTodayText();
            }
            await LoadUnofficialExpensesAsync(cancellationToken);
            if (!subscribedToFinancialChanges)
            {
                administrationService.DataChanged += OnAdministrationDataChanged;
                subscribedToFinancialChanges = true;
            }
            await RefreshFinancialSummaryAsync(cancellationToken);
            IsError = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnAdministrationDataChanged(object? sender, EventArgs eventArgs) => _ = RefreshFinancialSummaryAsync();

    private async Task RefreshFinancialSummaryAsync(CancellationToken cancellationToken = default)
    {
        if (!SelectedFinancialMonth.HasValue) return;
        FinancialMonthSnapshot summary = await administrationService.CalculateFinancialMonthAsync(
            YearMonth.From(DateOnly.FromDateTime(SelectedFinancialMonth.Value)), cancellationToken);
        FinancialSummaryRows.Clear();
        AddFinancialRow("Ingresos operativos cobrados", summary.CollectedOperatingIncomeMinorUnits);
        AddFinancialRow("Cuentas por cobrar", summary.AccountsReceivableMinorUnits);
        AddFinancialRow("Egresos pagados", summary.PaidOutflowsMinorUnits);
        AddFinancialRow("Cuentas por pagar", summary.AccountsPayableMinorUnits);
        AddFinancialRow("Reservas nuevas", summary.NewReservesMinorUnits);
        AddFinancialRow("Reservas arrastradas", summary.CarriedReservesMinorUnits);
        AddFinancialRow("Ajustes de reservas", summary.ReserveAdjustmentsMinorUnits);
        AddFinancialRow("Cuotas de préstamos", summary.LoanPaymentsMinorUnits);
        AddFinancialRow("Financiación recibida (no es ganancia)", summary.FinancingReceivedMinorUnits);
        AddFinancialRow("Resultado distribuible", summary.DistributableResultMinorUnits);
        AddFinancialRow("Punto de equilibrio", summary.BreakEvenMinorUnits);
        AddFinancialRow("Faltante", summary.ShortfallMinorUnits);
        AddFinancialRow("Fondo de colaboradores", summary.CollaboratorFundMinorUnits);
        AddFinancialRow("Retenido por el local", summary.RetainedLocalMinorUnits);
    }

    private void AddFinancialRow(string concept, long minorUnits) => FinancialSummaryRows.Add(
        FinancialSummaryRow.Create(concept, ApplicationCurrency.Code, minorUnits));

    partial void OnSelectedFinancialMonthChanged(DateTime? value) => _ = RefreshFinancialSummaryAsync();

    public async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            UpdateCheckResult result = await updateService.CheckAndDownloadAsync();
            UpdateReady = updateService.CanApplyUpdate;
            if (result.Status == UpdateCheckStatus.ReadyToInstall)
            {
                StatusMessage = $"La versión {result.Version} está descargada y lista para instalar.";
                IsError = false;
            }
        }
        catch
        {
            // Una falla de red o de GitHub nunca debe impedir el uso de la aplicación.
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        StatusMessage = string.Empty;
        IsError = false;

        if (!TryCreateRequest(out SaveSettingsRequest request, out string validationMessage))
        {
            StatusMessage = validationMessage;
            IsError = true;
            return;
        }

        IsBusy = true;
        try
        {
            SettingsDto settings = await saveSettings.ExecuteAsync(request, completedDraftKey: SettingsDraftKey);
            trackingEnabled = false;
            Apply(settings);
            trackingEnabled = true;
            StatusMessage = "Los ajustes se guardaron correctamente.";
        }
        catch (ArgumentException exception)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        catch (InvalidOperationException)
        {
            StatusMessage = "No fue posible guardar los ajustes. Verifica los datos e inténtalo de nuevo.";
            IsError = true;
        }
        catch (Exception exception)
        {
            StatusMessage = "No fue posible guardar los ajustes. Los datos anteriores se conservaron.";
#if DEBUG
            StatusMessage += $"{Environment.NewLine}{exception}";
#else
            _ = exception;
#endif
            IsError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task CreateBackupAsync()
    {
        await RunDataOperationAsync(
            dataManagement.CreateManualBackupAsync,
            path => $"Copia de seguridad creada correctamente:{Environment.NewLine}{path}",
            "No fue posible crear la copia de seguridad.");
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreBackupAsync()
    {
        string path = RestorePath.Trim();
        await RunDataOperationAsync(
            cancellationToken => dataManagement.RestoreAsync(path, cancellationToken),
            () => "La copia se restauró correctamente. Reinicia la aplicación para trabajar con los datos restaurados.",
            "No fue posible restaurar la copia. La base de datos anterior se conservó.");
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task ExportAllToExcelAsync()
    {
        IsBusy = true;
        StatusMessage = "Preparando la fotografía completa de los datos…";
        IsError = false;
        try
        {
            ExcelExportResult result = await excelExport.ExportAsync();
            lastExcelPath = result.FilePath;
            StatusMessage = $"Excel exportado correctamente:{Environment.NewLine}{result.FilePath}";
            OpenExcelFileCommand.NotifyCanExecuteChanged();
            OpenExcelFolderCommand.NotifyCanExecuteChanged();
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible exportar la información a Excel. No se dejó un archivo parcial. {exception.Message}";
            IsError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanOpenExcel))]
    private void OpenExcelFile() => OpenPath(lastExcelPath!, "No fue posible abrir el archivo Excel.");

    [RelayCommand(CanExecute = nameof(CanOpenExcel))]
    private void OpenExcelFolder() => OpenDirectory(Path.GetDirectoryName(lastExcelPath!)!);

    [RelayCommand]
    private void OpenBackups() => OpenDirectory(dataManagement.BackupsDirectory);

    [RelayCommand]
    private void ResetExportDirectory() => ExportDirectory = userDesktopPath.GetDesktopPath();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task CheckForUpdatesAsync()
    {
        IsBusy = true;
        StatusMessage = "Buscando actualizaciones…";
        IsError = false;
        try
        {
            UpdateCheckResult result = await updateService.CheckAndDownloadAsync();
            UpdateReady = updateService.CanApplyUpdate;
            StatusMessage = result.Status switch
            {
                UpdateCheckStatus.NotInstalled =>
                    "La búsqueda de actualizaciones está disponible en la aplicación instalada.",
                UpdateCheckStatus.UpToDate =>
                    $"La aplicación está actualizada ({result.Version ?? "versión actual"}).",
                UpdateCheckStatus.ReadyToInstall =>
                    $"La versión {result.Version} está descargada y lista para instalar.",
                _ => "La búsqueda de actualizaciones terminó.",
            };
        }
        catch (Exception exception)
        {
            SetDataError("No fue posible buscar actualizaciones. Puedes seguir usando la aplicación.", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanApplyUpdate))]
    private void ApplyUpdate()
    {
        try
        {
            updateService.ApplyAndRestart();
        }
        catch (Exception exception)
        {
            SetDataError("No fue posible iniciar la actualización.", exception);
            UpdateReady = updateService.CanApplyUpdate;
        }
    }

    private bool CanSave() => !IsBusy;

    private bool CanOpenExcel() => !IsBusy && !string.IsNullOrWhiteSpace(lastExcelPath) && File.Exists(lastExcelPath);

    private bool CanRestore() => !IsBusy && !string.IsNullOrWhiteSpace(RestorePath);

    private bool CanApplyUpdate() => !IsBusy && UpdateReady;

    public async Task FlushPendingAsync()
    {
        autosaveCancellation?.Cancel();
        unofficialEditCancellation?.Cancel();
        await autosaveLock.WaitAsync();
        autosaveLock.Release();
        await PersistUnofficialExpenseEditAsync();
    }

    private void TrackSettingsChange()
    {
        if (!trackingEnabled) return;
        _ = PersistSettingsDraftAsync();
        autosaveCancellation?.Cancel();
        autosaveCancellation = new CancellationTokenSource();
        _ = AutosaveSettingsAsync(autosaveCancellation.Token);
    }

    private async Task PersistSettingsDraftAsync()
    {
        await autosaveLock.WaitAsync();
        try
        {
            await formDraftStore.UpsertAsync(FormDraft.Create(
                SettingsDraftKey,
                "Ajustes",
                "Edición automática",
                JsonSerializer.Serialize(CurrentDraft()),
                null,
                true,
                timeProvider.GetUtcNow().UtcDateTime));
        }
        catch (Exception exception)
        {
            SetDataError("No fue posible conservar temporalmente los cambios de Ajustes.", exception);
        }
        finally
        {
            autosaveLock.Release();
        }
    }

    private async Task AutosaveSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(650, cancellationToken);
            if (!TryCreateRequest(out SaveSettingsRequest request, out string validationMessage))
            {
                StatusMessage = validationMessage;
                IsError = true;
                return;
            }

            await autosaveLock.WaitAsync(cancellationToken);
            autosaveLock.Release();
            SettingsDto settings = await saveSettings.ExecuteAsync(request, cancellationToken, SettingsDraftKey);
            trackingEnabled = false;
            Apply(settings);
            trackingEnabled = true;
            StatusMessage = "Ajustes guardados automáticamente.";
            IsError = false;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetDataError("No fue posible autoguardar los Ajustes; los campos escritos se conservaron.", exception);
        }
    }

    partial void OnWeeklyUsageFeeChanged(string value) => TrackSettingsChange();
    partial void OnCollaboratorProfitPercentChanged(string value) => TrackSettingsChange();
    partial void OnExportDirectoryChanged(string value) => TrackSettingsChange();

    partial void OnIsBusyChanged(bool value)
    {
        SaveCommand.NotifyCanExecuteChanged();
        CreateBackupCommand.NotifyCanExecuteChanged();
        RestoreBackupCommand.NotifyCanExecuteChanged();
        ExportAllToExcelCommand.NotifyCanExecuteChanged();
        OpenExcelFileCommand.NotifyCanExecuteChanged();
        OpenExcelFolderCommand.NotifyCanExecuteChanged();
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        ApplyUpdateCommand.NotifyCanExecuteChanged();
    }

    partial void OnRestorePathChanged(string value) => RestoreBackupCommand.NotifyCanExecuteChanged();

    partial void OnUpdateReadyChanged(bool value) => ApplyUpdateCommand.NotifyCanExecuteChanged();

    private SettingsDraft CurrentDraft() => new(
        WeeklyUsageFee,
        CollaboratorProfitPercent,
        ExportDirectory);

    private void ApplyDraft(SettingsDraft draft)
    {
        WeeklyUsageFee = draft.WeeklyUsageFee;
        CollaboratorProfitPercent = draft.CollaboratorProfitPercent;
        ExportDirectory = draft.ExportDirectory;
    }

    private sealed record SettingsDraft(
        string WeeklyUsageFee,
        string CollaboratorProfitPercent,
        string ExportDirectory);

    [RelayCommand]
    private async Task AddUnofficialExpenseAsync()
    {
        try
        {
            UnofficialExpense expense = UnofficialExpense.Create(
                UnofficialExpenseName,
                Money.FromDecimal(ParseUnofficialAmount()),
                ParseUnofficialDate(),
                UnofficialExpenseDescription,
                timeProvider.GetUtcNow().UtcDateTime);
            await administrationService.AddUnofficialExpenseAsync(expense);
            ClearUnofficialExpenseForm();
            await LoadUnofficialExpensesAsync();
            StatusMessage = "Gasto extraoficial agregado correctamente.";
            IsError = false;
        }
        catch (Exception exception)
        {
            SetDataError("No fue posible agregar el gasto extraoficial.", exception);
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedUnofficialExpense))]
    private async Task DeleteUnofficialExpenseAsync()
    {
        if (SelectedUnofficialExpense?.Entity is not UnofficialExpense expense) return;
        try
        {
            await administrationService.DeleteAsync(expense);
            ClearUnofficialExpenseForm();
            await LoadUnofficialExpensesAsync();
            StatusMessage = "Gasto extraoficial eliminado del estado vigente; su historial se conservó.";
            IsError = false;
        }
        catch (Exception exception)
        {
            SetDataError("No fue posible eliminar el gasto extraoficial.", exception);
        }
    }

    private bool HasSelectedUnofficialExpense() => SelectedUnofficialExpense?.Entity is UnofficialExpense;

    private bool CanDeleteUnofficialExpense() => HasSelectedUnofficialExpense() && ConfirmUnofficialExpenseDelete;

    partial void OnSelectedUnofficialExpenseChanged(OperationRow? value)
    {
        DeleteUnofficialExpenseCommand.NotifyCanExecuteChanged();
        if (value?.Entity is not UnofficialExpense expense) return;
        loadingUnofficialExpense = true;
        UnofficialExpenseName = expense.Name;
        UnofficialExpenseAmount = expense.MonthlyAmount.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
        UnofficialExpenseEffectiveFrom = expense.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        UnofficialExpenseDescription = expense.Description ?? string.Empty;
        loadingUnofficialExpense = false;
    }

    partial void OnUnofficialExpenseNameChanged(string value) => ScheduleUnofficialExpenseEdit();
    partial void OnUnofficialExpenseAmountChanged(string value) => ScheduleUnofficialExpenseEdit();
    partial void OnUnofficialExpenseEffectiveFromChanged(string value) => ScheduleUnofficialExpenseEdit();
    partial void OnUnofficialExpenseDescriptionChanged(string value) => ScheduleUnofficialExpenseEdit();

    partial void OnSelectedUnofficialPeriodChanged(string value)
    {
        ShowUnofficialCustomPeriod = value == "Rango personalizado";
        _ = LoadUnofficialExpensesAsync();
    }

    partial void OnUnofficialCustomFromChanged(DateTime? value)
    {
        if (ShowUnofficialCustomPeriod) _ = LoadUnofficialExpensesAsync();
    }

    partial void OnUnofficialCustomThroughChanged(DateTime? value)
    {
        if (ShowUnofficialCustomPeriod) _ = LoadUnofficialExpensesAsync();
    }

    partial void OnConfirmUnofficialExpenseDeleteChanged(bool value) =>
        DeleteUnofficialExpenseCommand.NotifyCanExecuteChanged();

    private async Task LoadUnofficialExpensesAsync(CancellationToken cancellationToken = default)
    {
        AdministrationData data = await administrationService.LoadAsync(cancellationToken);
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        ActivityDateRange range;
        try
        {
            range = ActivityPeriodCalculator.Calculate(
                ParsePeriod(SelectedUnofficialPeriod),
                today,
                UnofficialCustomFrom.HasValue ? DateOnly.FromDateTime(UnofficialCustomFrom.Value) : null,
                UnofficialCustomThrough.HasValue ? DateOnly.FromDateTime(UnofficialCustomThrough.Value) : null);
        }
        catch (ArgumentException exception)
        {
            UnofficialExpenses.Clear();
            UnofficialExpenseActivity.Clear();
            StatusMessage = exception.Message;
            IsError = true;
            return;
        }
        UnofficialExpenses.Clear();
        foreach (UnofficialExpense item in data.UnofficialExpenses.Where(item => range.Contains(item.EffectiveFrom)))
        {
            UnofficialExpenses.Add(new OperationRow(
                item.EffectiveFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.Name,
                item.Description ?? string.Empty,
                string.Empty,
                $"{ApplicationCurrency.Code} {item.MonthlyAmount.ToDecimal():N2}",
                "Extraoficial",
                item));
        }

        UnofficialExpenseActivity.Clear();
        foreach (var item in data.ActivityRecords
            .Where(item => item.Module == "Ajustes" && range.Contains(item.ActivityDate))
            .OrderByDescending(item => item.OccurredUtc))
        {
            UnofficialExpenseActivity.Add(new OperationRow(
                item.ActivityDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                item.Summary,
                item.Description ?? string.Empty,
                string.Empty,
                string.Empty,
                item.Action,
                null));
        }
    }

    private static ActivityPeriod ParsePeriod(string value) => value switch
    {
        "Hoy" => ActivityPeriod.Today,
        "Esta semana" => ActivityPeriod.ThisWeek,
        "Este mes" => ActivityPeriod.ThisMonth,
        "Últimos 3 meses" => ActivityPeriod.LastThreeMonths,
        "Últimos 6 meses" => ActivityPeriod.LastSixMonths,
        "Este año" => ActivityPeriod.ThisYear,
        "Rango personalizado" => ActivityPeriod.Custom,
        _ => ActivityPeriod.Today,
    };

    private decimal ParseUnofficialAmount() => TryParseDecimal(UnofficialExpenseAmount, out decimal amount)
        ? amount
        : throw new ArgumentException("El valor mensual debe ser un número válido.");

    private DateOnly ParseUnofficialDate() => DateOnly.TryParseExact(
        UnofficialExpenseEffectiveFrom,
        "yyyy-MM-dd",
        CultureInfo.InvariantCulture,
        DateTimeStyles.None,
        out DateOnly date)
        ? date
        : throw new ArgumentException("La fecha debe usar el formato AAAA-MM-DD.");

    private void ClearUnofficialExpenseForm()
    {
        loadingUnofficialExpense = true;
        SelectedUnofficialExpense = null;
        UnofficialExpenseName = string.Empty;
        UnofficialExpenseAmount = string.Empty;
        UnofficialExpenseEffectiveFrom = LocalTodayText();
        UnofficialExpenseDescription = string.Empty;
        ConfirmUnofficialExpenseDelete = false;
        loadingUnofficialExpense = false;
    }

    private void ScheduleUnofficialExpenseEdit()
    {
        if (loadingUnofficialExpense || SelectedUnofficialExpense?.Entity is not UnofficialExpense)
        {
            return;
        }

        unofficialEditCancellation?.Cancel();
        unofficialEditCancellation = new CancellationTokenSource();
        _ = PersistUnofficialExpenseEditAfterDelayAsync(unofficialEditCancellation.Token);
    }

    private async Task PersistUnofficialExpenseEditAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(650, cancellationToken);
            await PersistUnofficialExpenseEditAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PersistUnofficialExpenseEditAsync(CancellationToken cancellationToken = default)
    {
        if (loadingUnofficialExpense || SelectedUnofficialExpense?.Entity is not UnofficialExpense expense)
        {
            return;
        }

        try
        {
            string name = UnofficialExpenseName.Trim();
            if (string.IsNullOrWhiteSpace(name)
                || !TryParseDecimal(UnofficialExpenseAmount, out decimal amount)
                || amount < 0
                || !DateOnly.TryParseExact(UnofficialExpenseEffectiveFrom, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
            {
                StatusMessage = "Edición pendiente: completa valores válidos para autoguardar.";
                IsError = false;
                return;
            }

            expense.Update(name, Money.FromDecimal(amount), date, UnofficialExpenseDescription, timeProvider.GetUtcNow().UtcDateTime);
            await administrationService.UpdateAsync(expense, cancellationToken);
            Guid id = expense.Id;
            await LoadUnofficialExpensesAsync(cancellationToken);
            loadingUnofficialExpense = true;
            SelectedUnofficialExpense = UnofficialExpenses.SingleOrDefault(item => item.Entity?.Id == id);
            loadingUnofficialExpense = false;
            StatusMessage = "Gasto extraoficial guardado automáticamente.";
            IsError = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    private async Task RunDataOperationAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        Func<T, string> successMessage,
        string errorMessage)
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        IsError = false;
        try
        {
            T result = await operation(CancellationToken.None);
            StatusMessage = successMessage(result);
        }
        catch (Exception exception)
        {
            SetDataError(errorMessage, exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunDataOperationAsync(
        Func<CancellationToken, Task> operation,
        Func<string> successMessage,
        string errorMessage)
    {
        IsBusy = true;
        StatusMessage = string.Empty;
        IsError = false;
        try
        {
            await operation(CancellationToken.None);
            StatusMessage = successMessage();
        }
        catch (Exception exception)
        {
            SetDataError(errorMessage, exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SetDataError(string message, Exception exception)
    {
        StatusMessage = message;
#if DEBUG
        StatusMessage += $"{Environment.NewLine}{exception.Message}";
#else
        _ = exception;
#endif
        IsError = true;
    }

    private void OpenDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception exception)
        {
            SetDataError("No fue posible abrir la carpeta.", exception);
        }
    }

    private void OpenPath(string path, string errorMessage)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception exception)
        {
            SetDataError(errorMessage, exception);
        }
    }

    private bool TryCreateRequest(
        out SaveSettingsRequest request,
        out string validationMessage)
    {
        var errors = new List<string>();

        bool weeklyFeeIsValid = TryParseDecimal(WeeklyUsageFee, out decimal weeklyFee);
        if (!weeklyFeeIsValid)
        {
            errors.Add("El valor semanal debe ser un número válido.");
        }

        bool profitIsValid = TryParseDecimal(CollaboratorProfitPercent, out decimal profit);
        if (!profitIsValid)
        {
            errors.Add("La ganancia de colaboradores debe ser un número válido.");
        }

        string exportPath = ExportDirectory.Trim();
        if (string.IsNullOrWhiteSpace(exportPath))
        {
            errors.Add("Selecciona una carpeta de exportación.");
        }
        else
        {
            try
            {
                exportPath = Path.GetFullPath(exportPath);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                errors.Add("La carpeta de exportación no es válida.");
            }
        }

        if (errors.Count > 0)
        {
            request = null!;
            validationMessage = string.Join(Environment.NewLine, errors);
            return false;
        }

        request = new SaveSettingsRequest(weeklyFee, profit, exportPath);
        validationMessage = string.Empty;
        return true;
    }

    private static bool TryParseDecimal(string value, out decimal result)
    {
        const NumberStyles styles = NumberStyles.AllowLeadingWhite
            | NumberStyles.AllowTrailingWhite
            | NumberStyles.AllowLeadingSign
            | NumberStyles.AllowDecimalPoint;

        return decimal.TryParse(value, styles, CultureInfo.CurrentCulture, out result)
            || decimal.TryParse(value, styles, CultureInfo.InvariantCulture, out result);
    }

    private void Apply(SettingsDto settings)
    {
        WeeklyUsageFee = settings.WeeklyUsageFee.ToString("0.00", CultureInfo.CurrentCulture);
        CollaboratorProfitPercent = settings.CollaboratorProfitPercent.ToString("0.00", CultureInfo.CurrentCulture);
        ExportDirectory = string.IsNullOrWhiteSpace(settings.ExportDirectory)
            ? userDesktopPath.GetDesktopPath()
            : settings.ExportDirectory;
    }

    private string LocalTodayText() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime)
        .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}

public sealed record FinancialSummaryRow(string Concept, string Amount)
{
    public static FinancialSummaryRow Create(string concept, string currencyCode, long minorUnits) =>
        new(concept, $"{currencyCode} {minorUnits / 100m:N2}");
}
