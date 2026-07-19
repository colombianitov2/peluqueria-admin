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
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class LocalUseViewModel(
    AdministrationService service,
    GetSettingsUseCase getSettings,
    IFormDraftStore formDraftStore,
    TimeProvider timeProvider) : ObservableObject
{
    private const string Module = "Uso del local";
    private const string ActionDraftKey = "Uso del local:Fase42:accion";
    private CancellationTokenSource? draftCancellation;
    private CancellationTokenSource? workerEditCancellation;
    private CancellationTokenSource? chairEditCancellation;
    private bool suppressChanges;
    private Guid? profileWorkerId;

    public ObservableCollection<string> ActionOptions { get; } = ["Añadir silla", "Añadir trabajador"];

    public ObservableCollection<string> PeriodOptions { get; } =
        ["Hoy", "Esta semana", "Este mes", "Últimos 3 meses", "Últimos 6 meses", "Este año", "Rango personalizado"];

    public ObservableCollection<WorkerRow> Workers { get; } = [];

    public ObservableCollection<ChairRow> Chairs { get; } = [];

    public ObservableCollection<EntityOption> AvailableChairOptions { get; } = [];

    public ObservableCollection<OperationRow> ActivityRows { get; } = [];

    public ObservableCollection<OperationRow> WorkerHistoryRows { get; } = [];

    [ObservableProperty] private string selectedAction = "Añadir silla";
    [ObservableProperty] private string selectedPeriod = "Hoy";
    [ObservableProperty] private DateTime? customPeriodFrom = DateTime.Today;
    [ObservableProperty] private DateTime? customPeriodThrough = DateTime.Today;
    [ObservableProperty] private bool showCustomPeriod;
    [ObservableProperty] private int totalChairs;
    [ObservableProperty] private int currentWorkers;
    [ObservableProperty] private int availableChairs;
    [ObservableProperty] private string nameText = string.Empty;
    [ObservableProperty] private DateTime? actionDate = DateTime.Today;
    [ObservableProperty] private string descriptionText = string.Empty;
    [ObservableProperty] private EntityOption? selectedAvailableChair;
    [ObservableProperty] private bool isWorkerAction;
    [ObservableProperty] private WorkerRow? selectedWorkerRow;
    [ObservableProperty] private ChairRow? selectedChairRow;
    [ObservableProperty] private bool isWorkerProfileOpen;
    [ObservableProperty] private string profileName = string.Empty;
    [ObservableProperty] private string profileDescription = string.Empty;
    [ObservableProperty] private DateTime? profileEntryDate;
    [ObservableProperty] private DateTime? profileExitDate;
    [ObservableProperty] private string profileChair = "Sin silla asignada";
    [ObservableProperty] private string profileWeeklyRates = string.Empty;
    [ObservableProperty] private string profileDebt = string.Empty;
    [ObservableProperty] private DateTime? paymentDate = DateTime.Today;
    [ObservableProperty] private string paymentAmount = string.Empty;
    [ObservableProperty] private string paymentDescription = string.Empty;
    [ObservableProperty] private EntityOption? profileSelectedChair;
    [ObservableProperty] private DateTime? retirementDate = DateTime.Today;
    [ObservableProperty] private string chairEditName = string.Empty;
    [ObservableProperty] private DateTime? chairEditCreationDate;
    [ObservableProperty] private string chairEditDescription = string.Empty;
    [ObservableProperty] private bool confirmChairDelete;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isError;
    [ObservableProperty] private bool isBusy;

    public async Task LoadAsync()
    {
        await RefreshAsync();
        await RestoreActionDraftAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            DateOnly today = Today();
            AdministrationData data = await service.GenerateScheduledRecordsAsync(today);
            SettingsDto settings = await getSettings.ExecuteAsync();
            ActivityDateRange range = CurrentRange(today);

            Workers.Clear();
            foreach (LocalUsePerson worker in data.LocalUsePeople.OrderBy(item => item.Name))
            {
                Chair? chair = data.Chairs.SingleOrDefault(item => item.AssignedPersonId == worker.Id);
                Money debt = WeeklyChargeCalculator.CalculateDebt(
                    data.WeeklyCharges.Where(item => item.PersonId == worker.Id),
                    data.LocalUsePayments.Where(item => item.PersonId == worker.Id),
                    today);
                Workers.Add(new WorkerRow(
                    worker,
                    worker.Name,
                    worker.EntryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    chair?.Name ?? "Sin silla",
                    $"{settings.CurrencyCode} {debt.ToDecimal():N2}",
                    worker.IsCurrentOn(today) ? "Vigente" : "Retirado"));
            }

            if (IsWorkerProfileOpen && profileWorkerId.HasValue)
            {
                SelectedWorkerRow = Workers.SingleOrDefault(
                    item => item.Worker.Id == profileWorkerId.Value);
            }

            Chairs.Clear();
            foreach (Chair chair in data.Chairs.OrderBy(item => item.Name))
            {
                LocalUsePerson? assigned = chair.AssignedPersonId.HasValue
                    ? data.LocalUsePeople.SingleOrDefault(item => item.Id == chair.AssignedPersonId.Value)
                    : null;
                Chairs.Add(new ChairRow(
                    chair,
                    chair.Name,
                    chair.CreationDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    assigned?.Name ?? "Disponible",
                    chair.Description ?? string.Empty,
                    assigned is null ? "Disponible" : "Ocupada"));
            }

            AvailableChairOptions.Clear();
            foreach (Chair chair in data.Chairs
                .Where(item => !item.AssignedPersonId.HasValue)
                .OrderBy(item => item.Name))
            {
                AvailableChairOptions.Add(new EntityOption(chair.Id, chair.Name));
            }

            TotalChairs = data.Chairs.Count;
            CurrentWorkers = data.LocalUsePeople.Count(item => item.IsCurrentOn(today));
            AvailableChairs = data.Chairs.Count(item => !item.AssignedPersonId.HasValue);

            ActivityRows.Clear();
            foreach (var activity in data.ActivityRecords
                .Where(item => item.Module == Module && range.Contains(item.ActivityDate))
                .OrderByDescending(item => item.OccurredUtc))
            {
                ActivityRows.Add(new OperationRow(
                    activity.ActivityDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    activity.Action,
                    activity.Summary,
                    string.Empty,
                    string.Empty,
                    activity.Description ?? string.Empty,
                    activity));
            }

            if (IsWorkerProfileOpen)
            {
                await LoadWorkerProfileAsync(data, settings, range, today);
            }

            StatusMessage = string.Empty;
            IsError = false;
        }
        catch (Exception exception)
        {
            SetError("No fue posible cargar Uso del local.", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveActionAsync()
    {
        IsBusy = true;
        try
        {
            DateOnly date = RequiredDate(ActionDate, "fecha");
            DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
            if (SelectedAction == "Añadir silla")
            {
                await service.AddChairAsync(
                    Chair.Create(NameText, date, DescriptionText, utcNow),
                    completedDraftKey: ActionDraftKey);
            }
            else
            {
                if (SelectedAvailableChair is null)
                {
                    throw new InvalidOperationException(
                        "No hay sillas disponibles. Debes crear un espacio para una silla adicional.");
                }

                await service.AddLocalUsePersonWithChairAsync(
                    LocalUsePerson.Create(NameText, date, null, utcNow, DescriptionText),
                    SelectedAvailableChair.Id,
                    Today(),
                    completedDraftKey: ActionDraftKey);
            }

            ClearActionForm();
            StatusMessage = "La operación se guardó correctamente.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenSelectedWorkerProfileAsync()
    {
        if (SelectedWorkerRow is null)
        {
            return;
        }

        profileWorkerId = SelectedWorkerRow.Worker.Id;
        IsWorkerProfileOpen = true;
        suppressChanges = true;
        ProfileName = SelectedWorkerRow.Worker.Name;
        ProfileDescription = SelectedWorkerRow.Worker.Description ?? string.Empty;
        ProfileEntryDate = SelectedWorkerRow.Worker.EntryDate.ToDateTime(TimeOnly.MinValue);
        ProfileExitDate = SelectedWorkerRow.Worker.ExitDate?.ToDateTime(TimeOnly.MinValue);
        RetirementDate = DateTime.Today;
        suppressChanges = false;
        await RefreshAsync();
        await RestorePaymentDraftAsync();
    }

    [RelayCommand]
    private async Task CloseWorkerProfileAsync()
    {
        IsWorkerProfileOpen = false;
        profileWorkerId = null;
        SelectedWorkerRow = null;
        WorkerHistoryRows.Clear();
        ProfileSelectedChair = null;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task RegisterWorkerPaymentAsync()
    {
        if (SelectedWorkerRow is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            string key = PaymentDraftKey(SelectedWorkerRow.Worker.Id);
            await service.RegisterLocalUsePaymentAsync(
                SelectedWorkerRow.Worker.Id,
                RequiredDate(PaymentDate, "fecha de pago"),
                ParseMoney(PaymentAmount),
                completedDraftKey: key,
                description: PaymentDescription);
            PaymentAmount = string.Empty;
            PaymentDescription = string.Empty;
            StatusMessage = "El pago se registró correctamente.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AssignProfileChairAsync()
    {
        if (SelectedWorkerRow is null || ProfileSelectedChair is null)
        {
            StatusMessage = "Selecciona una silla disponible.";
            IsError = true;
            return;
        }

        await RunProfileOperationAsync(() => service.AssignChairAsync(
            SelectedWorkerRow.Worker.Id, ProfileSelectedChair.Id, Today()));
    }

    [RelayCommand]
    private async Task UnassignProfileChairAsync()
    {
        if (SelectedWorkerRow is null)
        {
            return;
        }

        await RunProfileOperationAsync(() => service.AssignChairAsync(
            SelectedWorkerRow.Worker.Id, null, Today()));
    }

    [RelayCommand]
    private async Task RetireWorkerAsync()
    {
        if (SelectedWorkerRow is null)
        {
            return;
        }

        await RunProfileOperationAsync(() => service.RetireLocalUsePersonAsync(
            SelectedWorkerRow.Worker.Id,
            RequiredDate(RetirementDate, "fecha de retiro")));
    }

    [RelayCommand]
    private async Task SaveSelectedChairAsync()
    {
        if (SelectedChairRow is null)
        {
            StatusMessage = "Selecciona una silla para editar.";
            IsError = true;
            return;
        }

        await RunProfileOperationAsync(() => service.UpdateChairAsync(
            SelectedChairRow.Chair.Id,
            ChairEditName,
            RequiredDate(ChairEditCreationDate, "fecha de creación"),
            ChairEditDescription));
    }

    [RelayCommand]
    private async Task DeleteSelectedChairAsync()
    {
        if (SelectedChairRow is null)
        {
            StatusMessage = "Selecciona una silla para eliminar.";
            IsError = true;
            return;
        }

        if (!ConfirmChairDelete)
        {
            StatusMessage = "Marca “Confirmo eliminar” antes de eliminar la silla.";
            IsError = true;
            return;
        }

        await RunProfileOperationAsync(() => service.DeleteAsync(SelectedChairRow.Chair));
        ConfirmChairDelete = false;
    }

    [RelayCommand]
    private async Task ClearFormAsync()
    {
        ClearActionForm();
        await formDraftStore.DeleteAsync(ActionDraftKey);
        if (SelectedWorkerRow is not null)
        {
            await formDraftStore.DeleteAsync(PaymentDraftKey(SelectedWorkerRow.Worker.Id));
        }
    }

    public async Task FlushPendingAsync()
    {
        draftCancellation?.Cancel();
        workerEditCancellation?.Cancel();
        chairEditCancellation?.Cancel();
        await PersistDraftAsync();
        await PersistWorkerEditAsync();
        await PersistChairEditAsync();
    }

    private async Task LoadWorkerProfileAsync(
        AdministrationData data,
        SettingsDto settings,
        ActivityDateRange range,
        DateOnly today)
    {
        if (SelectedWorkerRow is null)
        {
            IsWorkerProfileOpen = false;
            return;
        }

        Guid workerId = SelectedWorkerRow.Worker.Id;
        LocalUsePerson? worker = data.LocalUsePeople.SingleOrDefault(item => item.Id == workerId);
        if (worker is null)
        {
            await CloseWorkerProfileAsync();
            return;
        }

        Chair? currentChair = data.Chairs.SingleOrDefault(item => item.AssignedPersonId == workerId);
        ProfileChair = currentChair?.Name ?? "Sin silla asignada";
        Money debt = WeeklyChargeCalculator.CalculateDebt(
            data.WeeklyCharges.Where(item => item.PersonId == workerId),
            data.LocalUsePayments.Where(item => item.PersonId == workerId),
            today);
        ProfileDebt = $"{settings.CurrencyCode} {debt.ToDecimal():N2}";
        ProfileWeeklyRates = string.Join(" · ", data.WeeklyRates
            .OrderBy(item => item.EffectiveFrom)
            .Select(item => $"Desde {item.EffectiveFrom:yyyy-MM-dd}: {settings.CurrencyCode} {item.Amount.ToDecimal():N2}"));

        AvailableChairOptions.Clear();
        foreach (Chair chair in data.Chairs
            .Where(item => !item.AssignedPersonId.HasValue || item.AssignedPersonId == workerId)
            .OrderBy(item => item.Name))
        {
            AvailableChairOptions.Add(new EntityOption(chair.Id, chair.Name));
        }

        ProfileSelectedChair = currentChair is null
            ? null
            : AvailableChairOptions.SingleOrDefault(item => item.Id == currentChair.Id);

        var history = new List<(DateOnly Date, DateTime Order, OperationRow Row)>();
        if (range.Contains(worker.EntryDate))
        {
            history.Add((worker.EntryDate, worker.CreatedUtc, History(
                worker.EntryDate, "Ingreso al local", worker.Description, string.Empty, "Registrado", worker)));
        }

        foreach (var activity in data.ActivityRecords.Where(item => item.EntityId == workerId
            && item.Action != "Alta"
            && range.Contains(item.ActivityDate)))
        {
            history.Add((activity.ActivityDate, activity.OccurredUtc, History(
                activity.ActivityDate, activity.Action, activity.Summary, string.Empty,
                activity.Description ?? string.Empty, activity)));
        }

        foreach (WeeklyCharge charge in data.WeeklyCharges.Where(item => item.PersonId == workerId && range.Contains(item.PeriodEnd)))
        {
            history.Add((charge.PeriodEnd, charge.CreatedUtc, History(
                charge.PeriodEnd,
                "Cuota semanal generada",
                $"Periodo {charge.PeriodStart:yyyy-MM-dd} a {charge.PeriodEnd:yyyy-MM-dd}; pago habitual {charge.DueDate:yyyy-MM-dd} (sábado)",
                $"{settings.CurrencyCode} {charge.Amount.ToDecimal():N2}",
                "Pendiente o pagada según saldo",
                charge)));
        }

        foreach (LocalUsePayment payment in data.LocalUsePayments.Where(item => item.PersonId == workerId && range.Contains(item.PaymentDate)))
        {
            history.Add((payment.PaymentDate, payment.CreatedUtc, History(
                payment.PaymentDate,
                "Pago registrado",
                payment.Description,
                $"{settings.CurrencyCode} {payment.Amount.ToDecimal():N2}",
                payment.PaymentDate.DayOfWeek == DayOfWeek.Saturday ? "Pago habitual" : "Pago en otra fecha",
                payment)));
        }

        if (worker.ExitDate.HasValue && range.Contains(worker.ExitDate.Value))
        {
            history.Add((worker.ExitDate.Value, worker.UpdatedUtc, History(
                worker.ExitDate.Value, "Retiro del local", worker.Description, string.Empty, "Retirado", worker)));
        }

        WorkerHistoryRows.Clear();
        foreach (OperationRow row in history.OrderBy(item => item.Date).ThenBy(item => item.Order).Select(item => item.Row))
        {
            WorkerHistoryRows.Add(row);
        }
    }

    private async Task RunProfileOperationAsync(Func<Task> operation)
    {
        IsBusy = true;
        try
        {
            await operation();
            StatusMessage = "La operación se guardó correctamente.";
            IsError = false;
            await RefreshAsync();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private ActivityDateRange CurrentRange(DateOnly today) => ActivityPeriodCalculator.Calculate(
        SelectedPeriod switch
        {
            "Esta semana" => ActivityPeriod.ThisWeek,
            "Este mes" => ActivityPeriod.ThisMonth,
            "Últimos 3 meses" => ActivityPeriod.LastThreeMonths,
            "Últimos 6 meses" => ActivityPeriod.LastSixMonths,
            "Este año" => ActivityPeriod.ThisYear,
            "Rango personalizado" => ActivityPeriod.Custom,
            _ => ActivityPeriod.Today,
        },
        today,
        CustomPeriodFrom.HasValue ? DateOnly.FromDateTime(CustomPeriodFrom.Value) : null,
        CustomPeriodThrough.HasValue ? DateOnly.FromDateTime(CustomPeriodThrough.Value) : null);

    private static OperationRow History(
        DateOnly date,
        string operation,
        string? detail,
        string amount,
        string state,
        PeluqueriaAdmin.Domain.Common.AuditableEntity entity) => new(
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            operation,
            detail ?? string.Empty,
            string.Empty,
            amount,
            state,
            entity);

    private void ClearActionForm()
    {
        suppressChanges = true;
        NameText = string.Empty;
        ActionDate = DateTime.Today;
        DescriptionText = string.Empty;
        SelectedAvailableChair = null;
        PaymentAmount = string.Empty;
        PaymentDescription = string.Empty;
        suppressChanges = false;
    }

    private async Task RestoreActionDraftAsync()
    {
        FormDraft? draft = await formDraftStore.FindAsync(ActionDraftKey);
        if (draft is null) return;
        ActionDraftPayload? payload = JsonSerializer.Deserialize<ActionDraftPayload>(draft.PayloadJson);
        if (payload is null) return;
        suppressChanges = true;
        SelectedAction = ActionOptions.Contains(payload.Action) ? payload.Action : ActionOptions[0];
        IsWorkerAction = SelectedAction == "Añadir trabajador";
        NameText = payload.Name;
        ActionDate = payload.Date;
        DescriptionText = payload.Description;
        await RefreshAsync();
        SelectedAvailableChair = payload.ChairId.HasValue
            ? AvailableChairOptions.SingleOrDefault(item => item.Id == payload.ChairId.Value)
            : null;
        suppressChanges = false;
    }

    private async Task RestorePaymentDraftAsync()
    {
        if (SelectedWorkerRow is null) return;
        FormDraft? draft = await formDraftStore.FindAsync(PaymentDraftKey(SelectedWorkerRow.Worker.Id));
        if (draft is null) return;
        PaymentDraftPayload? payload = JsonSerializer.Deserialize<PaymentDraftPayload>(draft.PayloadJson);
        if (payload is null) return;
        suppressChanges = true;
        PaymentDate = payload.Date;
        PaymentAmount = payload.Amount;
        PaymentDescription = payload.Description;
        suppressChanges = false;
    }

    private void ScheduleDraft()
    {
        if (suppressChanges) return;
        draftCancellation?.Cancel();
        draftCancellation = new CancellationTokenSource();
        _ = PersistDraftAfterDelayAsync(draftCancellation.Token);
    }

    private async Task PersistDraftAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken);
            await PersistDraftAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PersistDraftAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(NameText) || !string.IsNullOrWhiteSpace(DescriptionText))
        {
            string payload = JsonSerializer.Serialize(new ActionDraftPayload(
                SelectedAction, NameText, ActionDate, DescriptionText, SelectedAvailableChair?.Id));
            await formDraftStore.UpsertAsync(FormDraft.Create(
                ActionDraftKey, Module, SelectedAction, payload, null, false,
                timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
        }

        if (SelectedWorkerRow is not null && (!string.IsNullOrWhiteSpace(PaymentAmount)
            || !string.IsNullOrWhiteSpace(PaymentDescription)))
        {
            string key = PaymentDraftKey(SelectedWorkerRow.Worker.Id);
            string payload = JsonSerializer.Serialize(new PaymentDraftPayload(
                PaymentDate, PaymentAmount, PaymentDescription));
            await formDraftStore.UpsertAsync(FormDraft.Create(
                key, Module, "Registrar pago", payload, SelectedWorkerRow.Worker.Id, false,
                timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
        }
    }

    private void ScheduleWorkerEdit()
    {
        if (suppressChanges || !IsWorkerProfileOpen || SelectedWorkerRow is null) return;
        workerEditCancellation?.Cancel();
        workerEditCancellation = new CancellationTokenSource();
        _ = PersistWorkerEditAfterDelayAsync(workerEditCancellation.Token);
    }

    private async Task PersistWorkerEditAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(650, cancellationToken);
            await PersistWorkerEditAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    private async Task PersistWorkerEditAsync(CancellationToken cancellationToken = default)
    {
        if (suppressChanges || SelectedWorkerRow is null || !ProfileEntryDate.HasValue) return;
        await service.UpdateLocalUsePersonAsync(
            SelectedWorkerRow.Worker.Id,
            ProfileName,
            DateOnly.FromDateTime(ProfileEntryDate.Value),
            ProfileExitDate.HasValue ? DateOnly.FromDateTime(ProfileExitDate.Value) : null,
            Today(),
            cancellationToken,
            description: ProfileDescription);
    }

    private void ScheduleChairEdit()
    {
        if (suppressChanges || SelectedChairRow is null) return;
        chairEditCancellation?.Cancel();
        chairEditCancellation = new CancellationTokenSource();
        _ = PersistChairEditAfterDelayAsync(chairEditCancellation.Token);
    }

    private async Task PersistChairEditAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(650, cancellationToken);
            await PersistChairEditAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    private async Task PersistChairEditAsync(CancellationToken cancellationToken = default)
    {
        if (suppressChanges || SelectedChairRow is null || !ChairEditCreationDate.HasValue) return;
        await service.UpdateChairAsync(
            SelectedChairRow.Chair.Id,
            ChairEditName,
            DateOnly.FromDateTime(ChairEditCreationDate.Value),
            ChairEditDescription,
            cancellationToken);
    }

    partial void OnSelectedActionChanged(string value)
    {
        IsWorkerAction = value == "Añadir trabajador";
        ScheduleDraft();
    }

    partial void OnSelectedPeriodChanged(string value)
    {
        ShowCustomPeriod = value == "Rango personalizado";
        _ = RefreshAsync();
    }

    partial void OnCustomPeriodFromChanged(DateTime? value) { if (ShowCustomPeriod) _ = RefreshAsync(); }
    partial void OnCustomPeriodThroughChanged(DateTime? value) { if (ShowCustomPeriod) _ = RefreshAsync(); }
    partial void OnNameTextChanged(string value) => ScheduleDraft();
    partial void OnActionDateChanged(DateTime? value) => ScheduleDraft();
    partial void OnDescriptionTextChanged(string value) => ScheduleDraft();
    partial void OnSelectedAvailableChairChanged(EntityOption? value) => ScheduleDraft();
    partial void OnPaymentDateChanged(DateTime? value) => ScheduleDraft();
    partial void OnPaymentAmountChanged(string value) => ScheduleDraft();
    partial void OnPaymentDescriptionChanged(string value) => ScheduleDraft();
    partial void OnProfileNameChanged(string value) => ScheduleWorkerEdit();
    partial void OnProfileDescriptionChanged(string value) => ScheduleWorkerEdit();
    partial void OnProfileEntryDateChanged(DateTime? value) => ScheduleWorkerEdit();
    partial void OnProfileExitDateChanged(DateTime? value) => ScheduleWorkerEdit();

    partial void OnSelectedChairRowChanged(ChairRow? value)
    {
        suppressChanges = true;
        ChairEditName = value?.Chair.Name ?? string.Empty;
        ChairEditCreationDate = value?.Chair.CreationDate.ToDateTime(TimeOnly.MinValue);
        ChairEditDescription = value?.Chair.Description ?? string.Empty;
        ConfirmChairDelete = false;
        suppressChanges = false;
    }

    partial void OnChairEditNameChanged(string value) => ScheduleChairEdit();
    partial void OnChairEditCreationDateChanged(DateTime? value) => ScheduleChairEdit();
    partial void OnChairEditDescriptionChanged(string value) => ScheduleChairEdit();

    private DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private static DateOnly RequiredDate(DateTime? value, string field) => value.HasValue
        ? DateOnly.FromDateTime(value.Value)
        : throw new ArgumentException($"La {field} es obligatoria.");

    private static Money ParseMoney(string value)
    {
        bool valid = decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal result)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
        return valid && result > 0
            ? Money.FromDecimal(result)
            : throw new ArgumentException("El valor debe ser mayor que cero y tener máximo dos decimales.");
    }

    private void SetError(string message, Exception exception)
    {
        StatusMessage = $"{message} {exception.Message}";
        IsError = true;
    }

    private static string PaymentDraftKey(Guid workerId) => $"Uso del local:Fase42:pago:{workerId:N}";

    private sealed record ActionDraftPayload(
        string Action,
        string Name,
        DateTime? Date,
        string Description,
        Guid? ChairId);

    private sealed record PaymentDraftPayload(DateTime? Date, string Amount, string Description);
}
