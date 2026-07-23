using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Activity;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Drafts;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Drafts;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class MaintenanceViewModel(
    AdministrationService service,
    GetSettingsUseCase getSettings,
    IFormDraftStore drafts,
    TimeProvider timeProvider) : ObservableObject
{
    private const string DraftKey = "Mantenimiento:Fase43:programacion";
    private CancellationTokenSource? draftCancellation;
    private bool suppressChanges;
    private bool suppressFilterRefresh;
    private readonly SemaphoreSlim refreshGate = new(1, 1);

    public ObservableCollection<string> FrequencyOptions { get; } =
        ["Una vez", "Semanal", "Quincenal", "Mensual", "Cada 2 meses", "Cada 3 meses", "Cada 6 meses", "Anual", "Personalizada"];
    public ObservableCollection<string> CustomUnitOptions { get; } = ["Días", "Semanas", "Meses", "Años"];
    public ObservableCollection<string> PeriodOptions { get; } =
        ["Hoy", "Esta semana", "Este mes", "Últimos 3 meses", "Últimos 6 meses", "Este año", "Todos", "Rango personalizado"];
    public ObservableCollection<MaintenanceRow> PendingRecords { get; } = [];
    public ObservableCollection<MaintenanceRow> HistoryRecords { get; } = [];
    public ObservableCollection<MaintenanceRow> PendingCompletionOptions { get; } = [];
    public ObservableCollection<string> HistoryAssetOptions { get; } = ["Historial de todos los equipos"];

    [ObservableProperty] private string assetText = string.Empty;
    [ObservableProperty] private string maintenanceTypeText = string.Empty;
    [ObservableProperty] private DateTime? scheduledDate = DateTime.Today;
    [ObservableProperty] private string estimatedCostText = string.Empty;
    [ObservableProperty] private string selectedFrequency = "Una vez";
    [ObservableProperty] private string customIntervalText = string.Empty;
    [ObservableProperty] private string selectedCustomUnit = "Días";
    [ObservableProperty] private bool showCustomInterval;
    [ObservableProperty] private string descriptionText = string.Empty;
    [ObservableProperty] private MaintenanceRow? selectedRow;
    [ObservableProperty] private MaintenanceRow? selectedPendingToComplete;
    [ObservableProperty] private string selectedHistoryAsset = "Historial de todos los equipos";
    [ObservableProperty] private bool isEditing;
    private Guid? editingMaintenanceId;
    [ObservableProperty] private DateTime? completedDate = DateTime.Today;
    [ObservableProperty] private string actualCostText = string.Empty;
    [ObservableProperty] private bool confirmStop;
    [ObservableProperty] private string selectedPeriod = "Todos";
    [ObservableProperty] private DateTime? customPeriodFrom = DateTime.Today;
    [ObservableProperty] private DateTime? customPeriodThrough = DateTime.Today;
    [ObservableProperty] private bool showCustomPeriod;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isError;
    [ObservableProperty] private bool isBusy;

    public async Task LoadAsync()
    {
        await RefreshAsync();
        await RestoreDraftAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await refreshGate.WaitAsync();
        IsBusy = true;
        try
        {
            AdministrationData data = await service.LoadAsync();
            SettingsDto settings = await getSettings.ExecuteAsync();
            ActivityDateRange? range = CurrentRange();
            Guid? selectedId = SelectedRow?.Record.Id;
            PendingRecords.Clear();
            PendingCompletionOptions.Clear();
            HistoryRecords.Clear();
            suppressFilterRefresh = true;
            try
            {
                HistoryAssetOptions.Clear();
                HistoryAssetOptions.Add("Historial de todos los equipos");
                foreach (string asset in data.MaintenanceRecords.Select(item => item.Asset).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item))
                {
                    HistoryAssetOptions.Add(asset);
                }
                if (!HistoryAssetOptions.Contains(SelectedHistoryAsset)) SelectedHistoryAsset = "Historial de todos los equipos";
            }
            finally { suppressFilterRefresh = false; }
            foreach (MaintenanceRecord record in data.MaintenanceRecords.OrderBy(item => item.ScheduledDate))
            {
                var row = ToRow(record);
                if (!record.CompletedDate.HasValue)
                {
                    PendingRecords.Add(row);
                    PendingCompletionOptions.Add(row);
                }
                else if ((!range.HasValue || range.Value.Contains(record.CompletedDate.Value))
                    && (SelectedHistoryAsset == "Historial de todos los equipos"
                        || string.Equals(record.Asset, SelectedHistoryAsset, StringComparison.OrdinalIgnoreCase)))
                {
                    HistoryRecords.Add(row);
                }
            }
            SelectedRow = selectedId.HasValue ? PendingRecords.SingleOrDefault(item => item.Record.Id == selectedId.Value) : null;
            StatusMessage = PendingRecords.Count == 0 && HistoryRecords.Count == 0 ? "Sin mantenimientos registrados." : string.Empty;
            IsError = false;
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible cargar Mantenimiento. {exception.Message}";
            IsError = true;
        }
        finally { IsBusy = false; refreshGate.Release(); }
    }

    [RelayCommand]
    private async Task ScheduleAsync()
    {
        IsBusy = true;
        try
        {
            MaintenanceFrequency frequency = ParseFrequency(SelectedFrequency);
            int? interval = frequency == MaintenanceFrequency.Custom ? ParsePositiveInt(CustomIntervalText) : null;
            MaintenanceIntervalUnit? unit = frequency == MaintenanceFrequency.Custom ? ParseUnit(SelectedCustomUnit) : null;
            MaintenanceRecord record = MaintenanceRecord.Schedule(
                AssetText, MaintenanceTypeText, RequiredDate(ScheduledDate, "fecha programada"),
                ParseOptionalMoney(EstimatedCostText), frequency, interval, unit,
                timeProvider.GetUtcNow().UtcDateTime, DescriptionText);
            await service.ScheduleMaintenanceAsync(record, completedDraftKey: DraftKey);
            ClearScheduleForm();
            StatusMessage = "El mantenimiento se programó correctamente.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (SelectedPendingToComplete is null)
        {
            StatusMessage = "Selecciona un mantenimiento pendiente.";
            IsError = true;
            return;
        }
        try
        {
            await service.CompleteMaintenanceAsync(
                SelectedPendingToComplete.Record.Id,
                RequiredDate(CompletedDate, "fecha realizada"),
                ParseRequiredMoney(ActualCostText),
                SelectedPendingToComplete.Record.Description);
            SelectedPendingToComplete = null;
            CompletedDate = timeProvider.GetLocalNow().DateTime.Date;
            ActualCostText = string.Empty;
            StatusMessage = "El mantenimiento se marcó como realizado.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    [RelayCommand]
    private void EditSelectedPending()
    {
        if (SelectedRow is null) return;
        MaintenanceRecord record = SelectedRow.Record;
        editingMaintenanceId = record.Id;
        IsEditing = true;
        suppressChanges = true;
        AssetText = record.Asset;
        MaintenanceTypeText = record.MaintenanceType;
        ScheduledDate = record.ScheduledDate.ToDateTime(TimeOnly.MinValue);
        EstimatedCostText = record.EstimatedCost?.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture) ?? string.Empty;
        SelectedFrequency = FrequencyName(record);
        DescriptionText = record.Description ?? string.Empty;
        suppressChanges = false;
    }

    [RelayCommand]
    private async Task SavePendingEditAsync()
    {
        if (!IsEditing || !editingMaintenanceId.HasValue) return;
        try
        {
            await service.UpdateMaintenanceAsync(
                editingMaintenanceId.Value, AssetText, MaintenanceTypeText,
                RequiredDate(ScheduledDate, "fecha programada"), ParseOptionalMoney(EstimatedCostText), DescriptionText);
            ClearScheduleForm();
            StatusMessage = "El mantenimiento pendiente se actualizó correctamente.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    [RelayCommand]
    private async Task StopFutureAsync()
    {
        if (SelectedRow is null || !ConfirmStop)
        {
            StatusMessage = "Selecciona una ocurrencia pendiente y marca la confirmación.";
            IsError = true;
            return;
        }
        try
        {
            await service.StopFutureMaintenanceAsync(SelectedRow.Record.Id);
            ConfirmStop = false;
            StatusMessage = "La ocurrencia futura se eliminó lógicamente; los mantenimientos realizados se conservan.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    public async Task FlushPendingAsync() { draftCancellation?.Cancel(); await PersistDraftAsync(); }

    private void ClearScheduleForm()
    {
        suppressChanges = true;
        AssetText = string.Empty; MaintenanceTypeText = string.Empty; ScheduledDate = DateTime.Today;
        EstimatedCostText = string.Empty; SelectedFrequency = "Una vez"; CustomIntervalText = string.Empty;
        SelectedCustomUnit = "Días"; DescriptionText = string.Empty;
        IsEditing = false; editingMaintenanceId = null;
        suppressChanges = false;
    }

    private void ScheduleDraft()
    {
        if (suppressChanges) return;
        draftCancellation?.Cancel();
        draftCancellation = new CancellationTokenSource();
        _ = PersistAfterDelayAsync(draftCancellation.Token);
    }

    private async Task PersistAfterDelayAsync(CancellationToken token)
    {
        try { await Task.Delay(650, token); await PersistDraftAsync(token); } catch (OperationCanceledException) { }
    }

    private async Task PersistDraftAsync(CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(AssetText) && string.IsNullOrWhiteSpace(MaintenanceTypeText)
            && string.IsNullOrWhiteSpace(EstimatedCostText) && string.IsNullOrWhiteSpace(DescriptionText))
        {
            await drafts.DeleteAsync(DraftKey, token); return;
        }
        var payload = new MaintenanceDraft(AssetText, MaintenanceTypeText, ScheduledDate, EstimatedCostText,
            SelectedFrequency, CustomIntervalText, SelectedCustomUnit, DescriptionText);
        string json = JsonSerializer.Serialize(payload);
        FormDraft? draft = await drafts.FindAsync(DraftKey, token);
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        if (draft is null) draft = FormDraft.Create(DraftKey, "Mantenimiento", "Programación", json, null, false, utcNow);
        else draft.UpdatePayload(json, utcNow);
        await drafts.UpsertAsync(draft, token);
    }

    private async Task RestoreDraftAsync()
    {
        FormDraft? draft = await drafts.FindAsync(DraftKey);
        if (draft is null) return;
        MaintenanceDraft? value = JsonSerializer.Deserialize<MaintenanceDraft>(draft.PayloadJson);
        if (value is null) return;
        suppressChanges = true;
        AssetText = value.Asset; MaintenanceTypeText = value.Type; ScheduledDate = value.Date;
        EstimatedCostText = value.EstimatedCost; SelectedFrequency = value.Frequency;
        CustomIntervalText = value.CustomInterval; SelectedCustomUnit = value.CustomUnit; DescriptionText = value.Description;
        ShowCustomInterval = SelectedFrequency == "Personalizada";
        suppressChanges = false;
        StatusMessage = "Se recuperó un borrador de mantenimiento sin registrar.";
    }

    partial void OnAssetTextChanged(string value) => ScheduleDraft();
    partial void OnMaintenanceTypeTextChanged(string value) => ScheduleDraft();
    partial void OnScheduledDateChanged(DateTime? value) => ScheduleDraft();
    partial void OnEstimatedCostTextChanged(string value) => ScheduleDraft();
    partial void OnSelectedFrequencyChanged(string value) { ShowCustomInterval = value == "Personalizada"; ScheduleDraft(); }
    partial void OnCustomIntervalTextChanged(string value) => ScheduleDraft();
    partial void OnSelectedCustomUnitChanged(string value) => ScheduleDraft();
    partial void OnDescriptionTextChanged(string value) => ScheduleDraft();
    partial void OnSelectedPeriodChanged(string value) { ShowCustomPeriod = value == "Rango personalizado"; if (!suppressFilterRefresh) _ = RefreshAsync(); }
    partial void OnCustomPeriodFromChanged(DateTime? value) { if (ShowCustomPeriod && !suppressFilterRefresh) _ = RefreshAsync(); }
    partial void OnCustomPeriodThroughChanged(DateTime? value) { if (ShowCustomPeriod && !suppressFilterRefresh) _ = RefreshAsync(); }
    partial void OnSelectedHistoryAssetChanged(string value) { if (!suppressFilterRefresh) _ = RefreshAsync(); }

    private ActivityDateRange? CurrentRange()
    {
        if (SelectedPeriod == "Todos") return null;
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        ActivityPeriod period = SelectedPeriod switch { "Esta semana" => ActivityPeriod.ThisWeek, "Este mes" => ActivityPeriod.ThisMonth, "Últimos 3 meses" => ActivityPeriod.LastThreeMonths, "Últimos 6 meses" => ActivityPeriod.LastSixMonths, "Este año" => ActivityPeriod.ThisYear, "Rango personalizado" => ActivityPeriod.Custom, _ => ActivityPeriod.Today };
        return ActivityPeriodCalculator.Calculate(period, today,
            CustomPeriodFrom.HasValue ? DateOnly.FromDateTime(CustomPeriodFrom.Value) : null,
            CustomPeriodThrough.HasValue ? DateOnly.FromDateTime(CustomPeriodThrough.Value) : null);
    }

    private static MaintenanceFrequency ParseFrequency(string value) => value switch { "Semanal" => MaintenanceFrequency.Weekly, "Quincenal" => MaintenanceFrequency.Biweekly, "Mensual" => MaintenanceFrequency.Monthly, "Cada 2 meses" => MaintenanceFrequency.EveryTwoMonths, "Cada 3 meses" => MaintenanceFrequency.EveryThreeMonths, "Cada 6 meses" => MaintenanceFrequency.EverySixMonths, "Anual" => MaintenanceFrequency.Yearly, "Personalizada" => MaintenanceFrequency.Custom, _ => MaintenanceFrequency.Once };
    private static MaintenanceIntervalUnit ParseUnit(string value) => value switch { "Semanas" => MaintenanceIntervalUnit.Weeks, "Meses" => MaintenanceIntervalUnit.Months, "Años" => MaintenanceIntervalUnit.Years, _ => MaintenanceIntervalUnit.Days };
    private static string FrequencyName(MaintenanceRecord value) => value.Frequency switch { MaintenanceFrequency.Weekly => "Semanal", MaintenanceFrequency.Biweekly => "Quincenal", MaintenanceFrequency.Monthly => "Mensual", MaintenanceFrequency.EveryTwoMonths => "Cada 2 meses", MaintenanceFrequency.EveryThreeMonths => "Cada 3 meses", MaintenanceFrequency.EverySixMonths => "Cada 6 meses", MaintenanceFrequency.Yearly => "Anual", MaintenanceFrequency.Custom => $"Cada {value.CustomInterval} {UnitName(value.CustomIntervalUnit)}", _ => "Una vez" };
    private static string UnitName(MaintenanceIntervalUnit? unit) => unit switch { MaintenanceIntervalUnit.Weeks => "semanas", MaintenanceIntervalUnit.Months => "meses", MaintenanceIntervalUnit.Years => "años", _ => "días" };
    private static MaintenanceRow ToRow(MaintenanceRecord record) => new(
        record,
        record.Asset,
        record.MaintenanceType,
        record.ScheduledDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        FrequencyName(record),
        record.EstimatedCost.HasValue ? $"{ApplicationCurrency.Code} {record.EstimatedCost.Value.ToDecimal():N2}" : string.Empty,
        record.CompletedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
        record.ActualCost.HasValue ? $"{ApplicationCurrency.Code} {record.ActualCost.Value.ToDecimal():N2}" : string.Empty,
        record.CompletedDate.HasValue ? "Realizado" : "Pendiente",
        record.Description ?? string.Empty);
    private static int ParsePositiveInt(string value) => int.TryParse(value, out int result) && result > 0 ? result : throw new ArgumentException("El intervalo personalizado debe ser un número entero mayor que cero.");
    private static DateOnly RequiredDate(DateTime? value, string field) => value.HasValue ? DateOnly.FromDateTime(value.Value) : throw new ArgumentException($"La {field} es obligatoria.");
    private static Money? ParseOptionalMoney(string value) => string.IsNullOrWhiteSpace(value) ? null : ParseRequiredMoney(value);
    private static Money ParseRequiredMoney(string value)
    {
        bool valid = decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal amount) || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
        return valid && amount >= 0 ? Money.FromDecimal(amount) : throw new ArgumentException("El valor debe ser un número igual o mayor que cero.");
    }
    private sealed record MaintenanceDraft(string Asset, string Type, DateTime? Date, string EstimatedCost, string Frequency, string CustomInterval, string CustomUnit, string Description);
}

public sealed record MaintenanceRow(MaintenanceRecord Record, string Asset, string Type, string ScheduledDate,
    string Frequency, string EstimatedCost, string CompletedDate, string ActualCost, string State, string Description)
{
    public string CompletionDisplay => $"{Asset} — {Type} — {ScheduledDate}";
}
