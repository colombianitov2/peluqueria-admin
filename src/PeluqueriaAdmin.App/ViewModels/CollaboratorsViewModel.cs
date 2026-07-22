using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeluqueriaAdmin.Application.Activity;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Drafts;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Drafts;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.ViewModels;

public sealed partial class CollaboratorsViewModel(
    AdministrationViewModel payrollEditor,
    AdministrationService service,
    GetSettingsUseCase getSettings,
    IFormDraftStore formDraftStore,
    TimeProvider timeProvider) : ObservableObject
{
    private const string Module = "Colaboradores";
    private const string AddDraftKey = "Colaboradores:Fase42:nuevo";
    private CancellationTokenSource? draftCancellation;
    private CancellationTokenSource? editCancellation;
    private CancellationTokenSource? distributionEditCancellation;
    private bool suppressChanges;
    private bool suppressDistributionChanges;
    private Guid? profileCollaboratorId;

    public AdministrationViewModel PayrollEditor { get; } = payrollEditor;

    public ObservableCollection<string> PeriodOptions { get; } =
        ["Hoy", "Esta semana", "Este mes", "Últimos 3 meses", "Últimos 6 meses", "Este año", "Rango personalizado"];

    public ObservableCollection<CollaboratorRow> Collaborators { get; } = [];

    public ObservableCollection<ContributionRow> Contributions { get; } = [];

    public ObservableCollection<OperationRow> ActivityRows { get; } = [];

    public ObservableCollection<OperationRow> HistoryRows { get; } = [];

    public ObservableCollection<CollaboratorDistributionOption> PendingDistributions { get; } = [];

    [ObservableProperty] private string selectedPeriod = "Hoy";
    [ObservableProperty] private DateTime? customPeriodFrom = DateTime.Today;
    [ObservableProperty] private DateTime? customPeriodThrough = DateTime.Today;
    [ObservableProperty] private bool showCustomPeriod;
    [ObservableProperty] private string newName = string.Empty;
    [ObservableProperty] private DateTime? newStartDate = DateTime.Today;
    [ObservableProperty] private DateTime? newExitDate;
    [ObservableProperty] private string newDescription = string.Empty;
    [ObservableProperty] private CollaboratorRow? selectedCollaboratorRow;
    [ObservableProperty] private string selectedProfitShareText = string.Empty;
    [ObservableProperty] private string globalProfitShare = string.Empty;
    [ObservableProperty] private string assignedProfitShare = string.Empty;
    [ObservableProperty] private string missingProfitShare = string.Empty;
    [ObservableProperty] private string totalProfitFund = string.Empty;
    [ObservableProperty] private string assignedProfitAmount = string.Empty;
    [ObservableProperty] private string pendingProfitAmount = string.Empty;
    [ObservableProperty] private bool isProfileOpen;
    [ObservableProperty] private string profileName = string.Empty;
    [ObservableProperty] private DateTime? profileStartDate;
    [ObservableProperty] private DateTime? profileExitDate;
    [ObservableProperty] private string profileDescription = string.Empty;
    [ObservableProperty] private string profileParticipationAmount = string.Empty;
    [ObservableProperty] private CollaboratorDistributionOption? selectedDistributionOption;
    [ObservableProperty] private DateTime? distributionPaymentDate = DateTime.Today;
    [ObservableProperty] private string distributionPaymentAmount = string.Empty;
    [ObservableProperty] private string distributionPaymentDescription = string.Empty;
    [ObservableProperty] private DateTime? contributionDate = DateTime.Today;
    [ObservableProperty] private string contributionAmount = string.Empty;
    [ObservableProperty] private string contributionDescription = string.Empty;
    [ObservableProperty] private ContributionRow? selectedContributionRow;
    [ObservableProperty] private bool isEditingContribution;
    [ObservableProperty] private bool confirmContributionDelete;
    [ObservableProperty] private bool confirmCollaboratorDelete;
    [ObservableProperty] private string statusMessage = string.Empty;
    [ObservableProperty] private bool isError;
    [ObservableProperty] private bool isBusy;

    public async Task LoadAsync()
    {
        await PayrollEditor.SelectModuleAsync(AdministrationViewModel.PayrollModule);
        await RefreshAsync();
        await RestoreAddDraftAsync();
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
            MonthlySummaryResult currentSummary = AdministrationReports.MonthlySummary(
                data,
                Percentage.FromPercent(settings.CollaboratorProfitPercent),
                YearMonth.From(today));
            long distributableBase = Math.Max(0, currentSummary.BaseResultMinorUnits);
            YearMonth currentMonth = YearMonth.From(today);
            MonthlyClose? confirmedClose = data.MonthlyCloses
                .Where(item => item.Month == currentMonth && item.IsConfirmed)
                .OrderByDescending(item => item.ClosedUtc)
                .FirstOrDefault();
            IReadOnlyDictionary<Guid, long> previewAmounts = confirmedClose is null
                ? CollaboratorDistributionCalculator.CalculateMinorUnitAmounts(
                    distributableBase,
                    checked((int)(settings.CollaboratorProfitPercent * 100m)),
                    data.Collaborators.Where(item => item.IsCurrentOn(today))
                        .Select(item => (item.Id, item.FundParticipationBasisPoints)))
                : data.MonthlyCloseParticipants.Where(item => item.CloseId == confirmedClose.Id)
                    .ToDictionary(item => item.CollaboratorId, item => item.Amount.MinorUnits);
            Guid? preservedCollaboratorId = SelectedCollaboratorRow?.Collaborator.Id;

            Collaborators.Clear();
            foreach (Collaborator collaborator in data.Collaborators.OrderBy(item => item.Name))
            {
                long assignedMinorUnits = previewAmounts.GetValueOrDefault(collaborator.Id);
                Collaborators.Add(new CollaboratorRow(
                    collaborator,
                    collaborator.Name,
                    collaborator.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    collaborator.ExitDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
                    collaborator.IsCurrentOn(today) ? "Vigente" : "Retirado",
                    collaborator.Description ?? string.Empty,
                    $"{collaborator.FundParticipationBasisPoints / 100m:N2} %",
                    $"{ApplicationCurrency.Code} {Money.FromMinorUnits(assignedMinorUnits).ToDecimal():N2}",
                    CurrentPaymentState(data, collaborator.Id, YearMonth.From(today))));
            }

            int assignedBasisPoints = data.Collaborators.Where(item => item.IsCurrentOn(today))
                .Sum(item => item.FundParticipationBasisPoints);
            long assignedTotalMinorUnits = previewAmounts.Values.Sum();
            GlobalProfitShare = $"Porcentaje global: {settings.CollaboratorProfitPercent:N2} %";
            AssignedProfitShare = $"Asignado: {assignedBasisPoints / 100m:N2} %";
            MissingProfitShare = $"Pendiente: {Math.Max(0m, 100m - assignedBasisPoints / 100m):N2} %";
            TotalProfitFund = $"Fondo total: {ApplicationCurrency.Code} {Money.FromMinorUnits(currentSummary.CollaboratorFundMinorUnits).ToDecimal():N2}";
            AssignedProfitAmount = $"Valor asignado: {ApplicationCurrency.Code} {Money.FromMinorUnits(assignedTotalMinorUnits).ToDecimal():N2}";
            PendingProfitAmount = $"Dinero pendiente: {ApplicationCurrency.Code} {Money.FromMinorUnits(Math.Max(0, currentSummary.CollaboratorFundMinorUnits - assignedTotalMinorUnits)).ToDecimal():N2}";

            suppressDistributionChanges = true;
            SelectedCollaboratorRow = preservedCollaboratorId.HasValue
                ? Collaborators.SingleOrDefault(item => item.Collaborator.Id == preservedCollaboratorId.Value)
                : null;
            SelectedProfitShareText = SelectedCollaboratorRow is null
                ? string.Empty
                : (SelectedCollaboratorRow.Collaborator.FundParticipationBasisPoints / 100m)
                    .ToString("0.##", CultureInfo.CurrentCulture);
            ProfileParticipationAmount = SelectedCollaboratorRow is null
                ? string.Empty
                : $"Valor correspondiente este mes: {SelectedCollaboratorRow.AssignedAmount}";
            suppressDistributionChanges = false;

            if (IsProfileOpen && profileCollaboratorId.HasValue)
            {
                SelectedCollaboratorRow = Collaborators.SingleOrDefault(
                    item => item.Collaborator.Id == profileCollaboratorId.Value);
            }

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

            if (IsProfileOpen)
            {
                LoadProfileHistory(data, settings, range);
            }

            StatusMessage = string.Empty;
            IsError = false;
        }
        catch (Exception exception)
        {
            StatusMessage = $"No fue posible cargar Colaboradores. {exception.Message}";
            IsError = true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddCollaboratorAsync()
    {
        IsBusy = true;
        try
        {
            await service.AddAsync(Collaborator.Create(
                NewName,
                RequiredDate(NewStartDate, "fecha de ingreso"),
                NewExitDate.HasValue ? DateOnly.FromDateTime(NewExitDate.Value) : null,
                timeProvider.GetUtcNow().UtcDateTime,
                NewDescription), completedDraftKey: AddDraftKey);
            ClearAddForm();
            StatusMessage = "El colaborador se añadió correctamente.";
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
    private async Task OpenSelectedProfileAsync()
    {
        if (SelectedCollaboratorRow is null) return;
        profileCollaboratorId = SelectedCollaboratorRow.Collaborator.Id;
        IsProfileOpen = true;
        suppressChanges = true;
        ProfileName = SelectedCollaboratorRow.Collaborator.Name;
        ProfileStartDate = SelectedCollaboratorRow.Collaborator.StartDate.ToDateTime(TimeOnly.MinValue);
        ProfileExitDate = SelectedCollaboratorRow.Collaborator.ExitDate?.ToDateTime(TimeOnly.MinValue);
        ProfileDescription = SelectedCollaboratorRow.Collaborator.Description ?? string.Empty;
        suppressChanges = false;
        await RefreshAsync();
        await RestoreContributionDraftAsync();
    }

    [RelayCommand]
    private void CloseProfile()
    {
        IsProfileOpen = false;
        profileCollaboratorId = null;
        SelectedCollaboratorRow = null;
        SelectedContributionRow = null;
        Contributions.Clear();
        HistoryRows.Clear();
    }

    [RelayCommand]
    private async Task DeleteCollaboratorAsync()
    {
        if (SelectedCollaboratorRow is null) return;
        if (!ConfirmCollaboratorDelete)
        {
            StatusMessage = "Marca “Confirmo eliminar” antes de eliminar al colaborador.";
            IsError = true;
            return;
        }

        Guid collaboratorId = SelectedCollaboratorRow.Collaborator.Id;
        await service.DeleteCollaboratorAsync(collaboratorId);
        ConfirmCollaboratorDelete = false;
        CloseProfile();
        StatusMessage = "El colaborador se eliminó lógicamente; su historial permanece conservado.";
        IsError = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task SaveContributionAsync()
    {
        if (SelectedCollaboratorRow is null) return;
        IsBusy = true;
        try
        {
            DateOnly date = RequiredDate(ContributionDate, "fecha del aporte");
            Money amount = ParseMoney(ContributionAmount);
            string draftKey = ContributionDraftKey(SelectedCollaboratorRow.Collaborator.Id);
            if (IsEditingContribution && SelectedContributionRow is not null)
            {
                await service.UpdateCollaboratorContributionAsync(
                    SelectedContributionRow.Contribution.Id,
                    date,
                    amount,
                    ContributionDescription,
                    completedDraftKey: draftKey);
            }
            else
            {
                await service.AddCollaboratorContributionAsync(
                    CollaboratorContribution.Create(
                        SelectedCollaboratorRow.Collaborator.Id,
                        date,
                        amount,
                        ContributionDescription,
                        timeProvider.GetUtcNow().UtcDateTime),
                    completedDraftKey: draftKey);
            }

            ClearContributionForm();
            StatusMessage = "El aporte se guardó correctamente como capital no operativo.";
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
    private async Task RegisterDistributionPaymentAsync()
    {
        if (SelectedDistributionOption is null)
        {
            StatusMessage = "Selecciona una participación pendiente.";
            IsError = true;
            return;
        }
        try
        {
            Money amount = ParsePositiveMoney(DistributionPaymentAmount, "El pago de ganancias");
            await service.RegisterDistributionPaymentAsync(
                SelectedDistributionOption.Participant.Id,
                RequiredDate(DistributionPaymentDate, "fecha del pago"),
                amount,
                description: DistributionPaymentDescription);
            DistributionPaymentDate = timeProvider.GetLocalNow().DateTime.Date;
            DistributionPaymentAmount = string.Empty;
            DistributionPaymentDescription = string.Empty;
            SelectedDistributionOption = null;
            StatusMessage = "El pago de ganancias se registró correctamente.";
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
    private void EditSelectedContribution()
    {
        if (SelectedContributionRow is null) return;
        suppressChanges = true;
        IsEditingContribution = true;
        ContributionDate = SelectedContributionRow.Contribution.Date.ToDateTime(TimeOnly.MinValue);
        ContributionAmount = SelectedContributionRow.Contribution.Amount.ToDecimal().ToString("0.00", CultureInfo.CurrentCulture);
        ContributionDescription = SelectedContributionRow.Contribution.Description ?? string.Empty;
        suppressChanges = false;
    }

    [RelayCommand]
    private async Task DeleteSelectedContributionAsync()
    {
        if (SelectedContributionRow is null)
        {
            StatusMessage = "Selecciona un aporte para eliminar.";
            IsError = true;
            return;
        }

        if (!ConfirmContributionDelete)
        {
            StatusMessage = "Marca “Confirmo eliminar” antes de eliminar el aporte.";
            IsError = true;
            return;
        }

        await service.DeleteAsync(SelectedContributionRow.Contribution);
        ConfirmContributionDelete = false;
        ClearContributionForm();
        StatusMessage = "El aporte se eliminó lógicamente y permanece en el historial eliminado.";
        IsError = false;
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ClearFormAsync()
    {
        ClearAddForm();
        ClearContributionForm();
        await formDraftStore.DeleteAsync(AddDraftKey);
        if (SelectedCollaboratorRow is not null)
        {
            await formDraftStore.DeleteAsync(ContributionDraftKey(SelectedCollaboratorRow.Collaborator.Id));
        }
    }

    public async Task FlushPendingAsync()
    {
        draftCancellation?.Cancel();
        editCancellation?.Cancel();
        distributionEditCancellation?.Cancel();
        await PersistDraftsAsync();
        await PersistProfileEditAsync();
        await PersistProfitShareAsync();
        await PayrollEditor.FlushPendingAsync();
    }

    private void LoadProfileHistory(AdministrationData data, SettingsDto settings, ActivityDateRange range)
    {
        if (SelectedCollaboratorRow is null)
        {
            IsProfileOpen = false;
            return;
        }

        Guid collaboratorId = SelectedCollaboratorRow.Collaborator.Id;
        Collaborator? collaborator = data.Collaborators.SingleOrDefault(item => item.Id == collaboratorId);
        if (collaborator is null)
        {
            CloseProfile();
            return;
        }

        Contributions.Clear();
        PendingDistributions.Clear();
        foreach (CollaboratorContribution contribution in data.CollaboratorContributions
            .Where(item => item.CollaboratorId == collaboratorId && range.Contains(item.Date))
            .OrderBy(item => item.Date).ThenBy(item => item.CreatedUtc))
        {
            Contributions.Add(new ContributionRow(
                contribution,
                contribution.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                $"{ApplicationCurrency.Code} {contribution.Amount.ToDecimal():N2}",
                contribution.Description ?? string.Empty,
                "Capital / inversión"));
        }

        var rows = new List<(DateOnly Date, DateTime Order, OperationRow Row)>();
        if (range.Contains(collaborator.StartDate))
        {
            rows.Add((collaborator.StartDate, collaborator.CreatedUtc, History(
                collaborator.StartDate, "Ingreso", collaborator.Description, string.Empty, "Registrado", collaborator)));
        }

        foreach (CollaboratorContribution contribution in data.CollaboratorContributions
            .Where(item => item.CollaboratorId == collaboratorId && range.Contains(item.Date)))
        {
            rows.Add((contribution.Date, contribution.CreatedUtc, History(
                contribution.Date,
                "Aporte de capital",
                contribution.Description,
                $"{ApplicationCurrency.Code} {contribution.Amount.ToDecimal():N2}",
                "No operativo",
                contribution)));
        }

        foreach (MonthlyCloseParticipant participant in data.MonthlyCloseParticipants.Where(item => item.CollaboratorId == collaboratorId))
        {
            MonthlyClose? close = data.MonthlyCloses.SingleOrDefault(item => item.Id == participant.CloseId && item.IsConfirmed);
            if (close is null) continue;
            long paidMinorUnits = data.DistributionPayments
                .Where(item => item.ParticipantId == participant.Id)
                .Sum(item => item.Amount.MinorUnits);
            long pendingMinorUnits = Math.Max(0, participant.Amount.MinorUnits - paidMinorUnits);
            if (pendingMinorUnits > 0)
            {
                PendingDistributions.Add(new CollaboratorDistributionOption(
                    participant,
                    $"{close.Month} — pendiente {ApplicationCurrency.Code} {pendingMinorUnits / 100m:N2}",
                    pendingMinorUnits));
            }
            if (!range.Contains(close.Month.LastDay)) continue;
            rows.Add((close.Month.LastDay, participant.CreatedUtc, History(
                close.Month.LastDay,
                "Participación calculada",
                $"Cierre {close.Month}",
                $"{ApplicationCurrency.Code} {participant.Amount.ToDecimal():N2}",
                "Calculada",
                participant)));
            foreach (DistributionPayment payment in data.DistributionPayments
                .Where(item => item.ParticipantId == participant.Id && range.Contains(item.Date)))
            {
                rows.Add((payment.Date, payment.CreatedUtc, History(
                    payment.Date,
                    "Pago de distribución",
                    payment.Description,
                    $"{ApplicationCurrency.Code} {payment.Amount.ToDecimal():N2}",
                    "Pagado",
                    payment)));
            }
        }

        foreach (var activity in data.ActivityRecords.Where(item => item.EntityId == collaboratorId
            && range.Contains(item.ActivityDate)
            && item.Action != "Alta"
            && (item.Summary != "Aporte de capital" || item.Action == "Eliminación")))
        {
            rows.Add((activity.ActivityDate, activity.OccurredUtc, History(
                activity.ActivityDate, activity.Action, activity.Summary, string.Empty,
                activity.Description ?? string.Empty, activity)));
        }

        if (collaborator.ExitDate.HasValue && range.Contains(collaborator.ExitDate.Value))
        {
            rows.Add((collaborator.ExitDate.Value, collaborator.UpdatedUtc, History(
                collaborator.ExitDate.Value, "Retiro", collaborator.Description, string.Empty, "Retirado", collaborator)));
        }

        HistoryRows.Clear();
        foreach (OperationRow row in rows.OrderBy(item => item.Date).ThenBy(item => item.Order).Select(item => item.Row))
        {
            HistoryRows.Add(row);
        }
    }

    private static OperationRow History(
        DateOnly date,
        string operation,
        string? detail,
        string amount,
        string state,
        PeluqueriaAdmin.Domain.Common.AuditableEntity entity) => new(
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), operation,
            detail ?? string.Empty, string.Empty, amount, state, entity);

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
            await PersistDraftsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PersistDraftsAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(NewName) || !string.IsNullOrWhiteSpace(NewDescription))
        {
            string payload = JsonSerializer.Serialize(new CollaboratorDraft(
                NewName, NewStartDate, NewExitDate, NewDescription));
            await formDraftStore.UpsertAsync(FormDraft.Create(
                AddDraftKey, Module, "Añadir colaborador", payload, null, false,
                timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
        }

        if (SelectedCollaboratorRow is not null && (!string.IsNullOrWhiteSpace(ContributionAmount)
            || !string.IsNullOrWhiteSpace(ContributionDescription)))
        {
            string key = ContributionDraftKey(SelectedCollaboratorRow.Collaborator.Id);
            string payload = JsonSerializer.Serialize(new ContributionDraft(
                ContributionDate, ContributionAmount, ContributionDescription));
            await formDraftStore.UpsertAsync(FormDraft.Create(
                key, Module, "Añadir aporte", payload, SelectedCollaboratorRow.Collaborator.Id, false,
                timeProvider.GetUtcNow().UtcDateTime), cancellationToken);
        }
    }

    private async Task RestoreAddDraftAsync()
    {
        FormDraft? draft = await formDraftStore.FindAsync(AddDraftKey);
        if (draft is null) return;
        CollaboratorDraft? payload = JsonSerializer.Deserialize<CollaboratorDraft>(draft.PayloadJson);
        if (payload is null) return;
        suppressChanges = true;
        NewName = payload.Name;
        NewStartDate = payload.StartDate;
        NewExitDate = payload.ExitDate;
        NewDescription = payload.Description;
        suppressChanges = false;
    }

    private async Task RestoreContributionDraftAsync()
    {
        if (SelectedCollaboratorRow is null) return;
        FormDraft? draft = await formDraftStore.FindAsync(ContributionDraftKey(SelectedCollaboratorRow.Collaborator.Id));
        if (draft is null) return;
        ContributionDraft? payload = JsonSerializer.Deserialize<ContributionDraft>(draft.PayloadJson);
        if (payload is null) return;
        suppressChanges = true;
        ContributionDate = payload.Date;
        ContributionAmount = payload.Amount;
        ContributionDescription = payload.Description;
        suppressChanges = false;
    }

    private void ScheduleProfileEdit()
    {
        if (suppressChanges || !IsProfileOpen || SelectedCollaboratorRow is null) return;
        editCancellation?.Cancel();
        editCancellation = new CancellationTokenSource();
        _ = PersistProfileEditAfterDelayAsync(editCancellation.Token);
    }

    private async Task PersistProfileEditAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(650, cancellationToken);
            await PersistProfileEditAsync(cancellationToken);
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

    private async Task PersistProfileEditAsync(CancellationToken cancellationToken = default)
    {
        if (suppressChanges || SelectedCollaboratorRow is null || !ProfileStartDate.HasValue) return;
        Collaborator collaborator = SelectedCollaboratorRow.Collaborator;
        collaborator.Update(
            ProfileName,
            DateOnly.FromDateTime(ProfileStartDate.Value),
            ProfileExitDate.HasValue ? DateOnly.FromDateTime(ProfileExitDate.Value) : null,
            timeProvider.GetUtcNow().UtcDateTime,
            ProfileDescription);
        await service.UpdateAsync(collaborator, cancellationToken);
    }

    private void ClearAddForm()
    {
        suppressChanges = true;
        NewName = string.Empty;
        NewStartDate = DateTime.Today;
        NewExitDate = null;
        NewDescription = string.Empty;
        suppressChanges = false;
    }

    private void ClearContributionForm()
    {
        suppressChanges = true;
        ContributionDate = DateTime.Today;
        ContributionAmount = string.Empty;
        ContributionDescription = string.Empty;
        SelectedContributionRow = null;
        IsEditingContribution = false;
        suppressChanges = false;
    }

    partial void OnSelectedPeriodChanged(string value)
    {
        ShowCustomPeriod = value == "Rango personalizado";
        _ = RefreshAsync();
    }

    partial void OnCustomPeriodFromChanged(DateTime? value) { if (ShowCustomPeriod) _ = RefreshAsync(); }
    partial void OnCustomPeriodThroughChanged(DateTime? value) { if (ShowCustomPeriod) _ = RefreshAsync(); }
    partial void OnNewNameChanged(string value) => ScheduleDraft();
    partial void OnNewStartDateChanged(DateTime? value) => ScheduleDraft();
    partial void OnNewExitDateChanged(DateTime? value) => ScheduleDraft();
    partial void OnNewDescriptionChanged(string value) => ScheduleDraft();
    partial void OnContributionDateChanged(DateTime? value) => ScheduleDraft();
    partial void OnContributionAmountChanged(string value) => ScheduleDraft();
    partial void OnContributionDescriptionChanged(string value) => ScheduleDraft();
    partial void OnProfileNameChanged(string value) => ScheduleProfileEdit();
    partial void OnProfileStartDateChanged(DateTime? value) => ScheduleProfileEdit();
    partial void OnProfileExitDateChanged(DateTime? value) => ScheduleProfileEdit();
    partial void OnProfileDescriptionChanged(string value) => ScheduleProfileEdit();
    partial void OnSelectedCollaboratorRowChanged(CollaboratorRow? value)
    {
        if (suppressDistributionChanges) return;
        suppressDistributionChanges = true;
        SelectedProfitShareText = value is null
            ? string.Empty
            : (value.Collaborator.FundParticipationBasisPoints / 100m).ToString("0.##", CultureInfo.CurrentCulture);
        ProfileParticipationAmount = value is null
            ? string.Empty
            : $"Valor correspondiente este mes: {value.AssignedAmount}";
        suppressDistributionChanges = false;
    }

    partial void OnSelectedProfitShareTextChanged(string value)
    {
        if (suppressDistributionChanges || SelectedCollaboratorRow is null) return;
        distributionEditCancellation?.Cancel();
        distributionEditCancellation = new CancellationTokenSource();
        _ = PersistProfitShareAfterDelayAsync(distributionEditCancellation.Token);
    }

    private async Task PersistProfitShareAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(650, cancellationToken);
            await PersistProfitShareAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task PersistProfitShareAsync(CancellationToken cancellationToken = default)
    {
        CollaboratorRow? selected = SelectedCollaboratorRow;
        if (selected is null || !TryParsePercentage(SelectedProfitShareText, out decimal percent)) return;
        try
        {
            await service.UpdateCollaboratorFundParticipationAsync(
                selected.Collaborator.Id,
                Percentage.FromPercent(percent),
                cancellationToken);
            await RefreshAsync();
            StatusMessage = "Guardado";
            IsError = false;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
            IsError = true;
        }
    }

    private static bool TryParsePercentage(string value, out decimal percent)
    {
        bool parsed = decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out percent)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out percent);
        return parsed && percent is >= 0m and <= 100m;
    }

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
            : throw new ArgumentException("El aporte debe ser mayor que cero y tener máximo dos decimales.");
    }

    private static Money ParsePositiveMoney(string value, string field)
    {
        bool valid = decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out decimal result)
            || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result);
        return valid && result > 0
            ? Money.FromDecimal(result)
            : throw new ArgumentException($"{field} debe ser mayor que cero y tener máximo dos decimales.");
    }

    private static string CurrentPaymentState(AdministrationData data, Guid collaboratorId, YearMonth month)
    {
        MonthlyClose? close = data.MonthlyCloses
            .Where(item => item.Month == month && item.IsConfirmed)
            .OrderByDescending(item => item.ClosedUtc)
            .FirstOrDefault();
        if (close is null) return "Mes abierto";
        MonthlyCloseParticipant? participant = data.MonthlyCloseParticipants
            .SingleOrDefault(item => item.CloseId == close.Id && item.CollaboratorId == collaboratorId);
        if (participant is null) return "Sin asignación";
        long paid = data.DistributionPayments.Where(item => item.ParticipantId == participant.Id)
            .Sum(item => item.Amount.MinorUnits);
        return paid >= participant.Amount.MinorUnits ? "Pagado" : paid > 0 ? "Pago parcial" : "Pendiente";
    }

    private static string ContributionDraftKey(Guid collaboratorId) => $"Colaboradores:Fase42:aporte:{collaboratorId:N}";

    private sealed record CollaboratorDraft(string Name, DateTime? StartDate, DateTime? ExitDate, string Description);

    private sealed record ContributionDraft(DateTime? Date, string Amount, string Description);
}
