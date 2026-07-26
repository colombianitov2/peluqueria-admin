using System.Globalization;
using PeluqueriaAdmin.App.ViewModels;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Drafts;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Activity;
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

namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase50AContributionWorkflowTests
{
    private static readonly DateTime Utc =
        new(2026, 7, 25, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SelectingVisibleContributionEvent_LoadsAndEditsTheExactActiveContributionImmediately()
    {
        WorkflowContext context = await CreateContextAsync();
        OperationRow secondContributionEvent = context.ViewModel.HistoryRows.Single(
            row => row.Entity is CollaboratorContributionEvent contributionEvent
                && contributionEvent.ContributionId == context.SecondContribution.Id
                && contributionEvent.EventType == CollaboratorContributionEventType.Created);

        context.ViewModel.SelectedHistoryRow = secondContributionEvent;

        Assert.Equal(context.SecondContribution.Id, context.ViewModel.SelectedContributionRow?.Contribution.Id);
        Assert.Equal(new DateTime(2026, 7, 25), context.ViewModel.ContributionDate);
        Assert.Equal(25m.ToString("0.00", CultureInfo.CurrentCulture),
            context.ViewModel.ContributionAmount);
        Assert.Equal("Segundo aporte", context.ViewModel.ContributionDescription);
        Assert.True(context.ViewModel.IsEditingContribution);
        Assert.True(context.ViewModel.EditSelectedContributionCommand.CanExecute(null));

        context.ViewModel.ContributionDate = new DateTime(2026, 7, 24);
        context.ViewModel.ContributionAmount = 35.50m.ToString("0.00", CultureInfo.CurrentCulture);
        context.ViewModel.ContributionDescription = "Segundo aporte corregido";
        await context.ViewModel.SaveContributionCommand.ExecuteAsync(null);

        CollaboratorContribution saved = context.Repository.Active<CollaboratorContribution>()
            .Single(item => item.Id == context.SecondContribution.Id);
        Assert.Equal(new DateOnly(2026, 7, 24), saved.Date);
        Assert.Equal(3_550, saved.Amount.MinorUnits);
        Assert.Equal("Segundo aporte corregido", saved.Description);
        Assert.Equal(1_000, context.Repository.Active<CollaboratorContribution>()
            .Single(item => item.Id == context.FirstContribution.Id).Amount.MinorUnits);
        Assert.Contains(context.Repository.Active<CollaboratorContributionEvent>(),
            item => item.ContributionId == context.SecondContribution.Id
                && item.EventType == CollaboratorContributionEventType.Edited
                && item.PreviousEffectiveDate == new DateOnly(2026, 7, 25)
                && item.EffectiveDate == new DateOnly(2026, 7, 24)
                && item.PreviousAmount?.MinorUnits == 2_500
                && item.Amount.MinorUnits == 3_550
                && item.Description == "Segundo aporte corregido");
        Assert.Contains(context.ViewModel.HistoryRows,
            row => row.Principal == "Aporte original"
                && row.Entity is CollaboratorContributionEvent original
                && original.ContributionId == context.SecondContribution.Id);
        Assert.Contains(context.ViewModel.HistoryRows,
            row => row.Principal == "Aporte editado"
                && row.Detail.Contains("Segundo aporte corregido", StringComparison.Ordinal));
        Assert.Contains("45", Assert.Single(context.ViewModel.Collaborators).TotalContributed,
            StringComparison.Ordinal);
        Assert.Null(context.ViewModel.SelectedHistoryRow);
        Assert.Null(context.ViewModel.SelectedContributionRow);
        Assert.False(context.ViewModel.EditSelectedContributionCommand.CanExecute(null));
        Assert.False(context.ViewModel.DeleteSelectedContributionCommand.CanExecute(null));
    }

    [Fact]
    public async Task DeletingSelectedContribution_ExcludesItFromTotalsAndKeepsCreationAndDeletionEvents()
    {
        WorkflowContext context = await CreateContextAsync();
        context.ViewModel.SelectedHistoryRow = context.ViewModel.HistoryRows.Single(
            row => row.Entity is CollaboratorContributionEvent contributionEvent
                && contributionEvent.ContributionId == context.SecondContribution.Id
                && contributionEvent.EventType == CollaboratorContributionEventType.Created);
        context.ViewModel.ConfirmContributionDelete = true;

        Assert.True(context.ViewModel.DeleteSelectedContributionCommand.CanExecute(null));
        await context.ViewModel.DeleteSelectedContributionCommand.ExecuteAsync(null);

        Assert.DoesNotContain(context.Repository.Active<CollaboratorContribution>(),
            item => item.Id == context.SecondContribution.Id);
        Assert.Equal(1_000, Assert.Single(context.Repository.Active<CollaboratorContribution>()).Amount.MinorUnits);
        CollaboratorContributionEvent[] events = context.Repository.Active<CollaboratorContributionEvent>()
            .Where(item => item.ContributionId == context.SecondContribution.Id)
            .ToArray();
        Assert.Contains(events, item => item.EventType == CollaboratorContributionEventType.Created);
        Assert.Contains(events, item => item.EventType == CollaboratorContributionEventType.Deleted);
        OperationRow deletedHistory = Assert.Single(context.ViewModel.HistoryRows,
            row => row.Principal == "Aporte eliminado"
                && row.Entity is CollaboratorContributionEvent deleted
                && deleted.ContributionId == context.SecondContribution.Id);
        Assert.Contains("Eliminado", deletedHistory.Status, StringComparison.Ordinal);
        Assert.Contains("10", Assert.Single(context.ViewModel.Collaborators).TotalContributed,
            StringComparison.Ordinal);
        Assert.Null(context.ViewModel.SelectedHistoryRow);
        Assert.Null(context.ViewModel.SelectedContributionRow);
        Assert.False(context.ViewModel.ConfirmContributionDelete);
        Assert.False(context.ViewModel.EditSelectedContributionCommand.CanExecute(null));
        Assert.False(context.ViewModel.DeleteSelectedContributionCommand.CanExecute(null));

        context.ViewModel.SelectedHistoryRow = deletedHistory;
        context.ViewModel.ConfirmContributionDelete = true;
        Assert.Null(context.ViewModel.SelectedContributionRow);
        Assert.False(context.ViewModel.EditSelectedContributionCommand.CanExecute(null));
        Assert.False(context.ViewModel.DeleteSelectedContributionCommand.CanExecute(null));
    }

    [Fact]
    public async Task MissingOrNonContributionSelection_NeverEnablesContributionActions()
    {
        WorkflowContext context = await CreateContextAsync();

        context.ViewModel.ConfirmContributionDelete = true;
        Assert.False(context.ViewModel.EditSelectedContributionCommand.CanExecute(null));
        Assert.False(context.ViewModel.DeleteSelectedContributionCommand.CanExecute(null));

        context.ViewModel.SelectedHistoryRow = context.ViewModel.HistoryRows.Single(
            row => row.Entity is Collaborator);

        Assert.Null(context.ViewModel.SelectedContributionRow);
        Assert.False(context.ViewModel.ConfirmContributionDelete);
        Assert.False(context.ViewModel.IsEditingContribution);
        Assert.False(context.ViewModel.EditSelectedContributionCommand.CanExecute(null));
        Assert.False(context.ViewModel.DeleteSelectedContributionCommand.CanExecute(null));
    }

    [Fact]
    public async Task EditingOrDeletingCapitalContribution_DoesNotChangeExistingDistributionFormula()
    {
        WorkflowContext context = await CreateContextAsync();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await context.Service.AddAsync(FinancialEntry.CreateIncome(
            new DateOnly(2026, 7, 25),
            "Ingreso operativo controlado",
            Money.FromDecimal(100m),
            Utc),
            cancellationToken);
        FinancialMonthSnapshot baseline = FinancialMonthCalculator.Calculate(
            await context.Service.LoadAsync(cancellationToken),
            Percentage.FromPercent(20m),
            new YearMonth(2026, 7));

        await context.Service.UpdateCollaboratorContributionAsync(
            context.SecondContribution.Id,
            new DateOnly(2026, 7, 24),
            Money.FromDecimal(250m),
            "Capital corregido",
            cancellationToken);
        FinancialMonthSnapshot afterEdit = FinancialMonthCalculator.Calculate(
            await context.Service.LoadAsync(cancellationToken),
            Percentage.FromPercent(20m),
            new YearMonth(2026, 7));
        await context.Service.DeleteCollaboratorContributionAsync(
            context.SecondContribution.Id,
            cancellationToken);
        FinancialMonthSnapshot afterDelete = FinancialMonthCalculator.Calculate(
            await context.Service.LoadAsync(cancellationToken),
            Percentage.FromPercent(20m),
            new YearMonth(2026, 7));

        Assert.Equal(10_000, baseline.DistributableResultMinorUnits);
        Assert.Equal(2_000, baseline.CollaboratorFundMinorUnits);
        Assert.Equal(baseline.DistributableResultMinorUnits, afterEdit.DistributableResultMinorUnits);
        Assert.Equal(baseline.CollaboratorFundMinorUnits, afterEdit.CollaboratorFundMinorUnits);
        Assert.Equal(baseline.DistributableResultMinorUnits, afterDelete.DistributableResultMinorUnits);
        Assert.Equal(baseline.CollaboratorFundMinorUnits, afterDelete.CollaboratorFundMinorUnits);
    }

    private static async Task<WorkflowContext> CreateContextAsync()
    {
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(Utc));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(Utc));
        var drafts = new FakeFormDraftStore();
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        Collaborator collaborator = Collaborator.Create(
            "Inversionista de prueba", new DateOnly(2026, 1, 1), null, Utc);
        await service.AddAsync(collaborator);
        CollaboratorContribution firstContribution = CollaboratorContribution.Create(
            collaborator.Id, new DateOnly(2026, 7, 25), Money.FromDecimal(10m), "Primer aporte", Utc);
        CollaboratorContribution secondContribution = CollaboratorContribution.Create(
            collaborator.Id, new DateOnly(2026, 7, 25), Money.FromDecimal(25m), "Segundo aporte", Utc.AddMinutes(1));
        await service.AddCollaboratorContributionAsync(firstContribution);
        await service.AddCollaboratorContributionAsync(secondContribution);
        var editor = new AdministrationViewModel(
            service, new GetSettingsUseCase(settingsRepository), drafts, timeProvider);
        var viewModel = new CollaboratorsViewModel(
            editor, service, new GetSettingsUseCase(settingsRepository), drafts, timeProvider)
        {
            SelectedPeriod = "Este año",
        };
        await viewModel.LoadAsync();
        viewModel.SelectedCollaboratorRow = Assert.Single(viewModel.Collaborators);
        await viewModel.OpenSelectedProfileCommand.ExecuteAsync(null);
        return new WorkflowContext(
            viewModel, service, repository, collaborator, firstContribution, secondContribution);
    }

    private sealed record WorkflowContext(
        CollaboratorsViewModel ViewModel,
        AdministrationService Service,
        FakeAdministrationRepository Repository,
        Collaborator Collaborator,
        CollaboratorContribution FirstContribution,
        CollaboratorContribution SecondContribution);

    private sealed class FakeSettingsRepository(GeneralSettings settings) : ISettingsRepository
    {
        public Task<GeneralSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);

        public Task SaveAsync(GeneralSettings value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeFormDraftStore : IFormDraftStore
    {
        private readonly Dictionary<string, FormDraft> drafts = [];

        public Task<IReadOnlyList<FormDraft>> LoadAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FormDraft>>(drafts.Values.ToArray());

        public Task<FormDraft?> FindAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(drafts.GetValueOrDefault(key));

        public Task UpsertAsync(FormDraft draft, CancellationToken cancellationToken = default)
        {
            drafts[draft.Key] = draft;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            drafts.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAdministrationRepository : IAdministrationRepository
    {
        private readonly List<AuditableEntity> entities = [];

        public T[] Active<T>() where T : AuditableEntity => entities
            .OfType<T>()
            .Where(item => !item.IsDeleted)
            .ToArray();

        public Task<AdministrationData> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdministrationData(
                Active<LocalUsePerson>(),
                Active<WeeklyRate>(),
                Active<WeeklyCharge>(),
                Active<LocalUsePayment>(),
                Active<Product>(),
                Active<InventoryMovement>(),
                Active<MonthlyRestockPlan>(),
                Active<FinancialEntry>(),
                Active<Obligation>(),
                Active<ObligationPayment>(),
                Active<MaintenanceRecord>(),
                Active<Collaborator>(),
                Active<MonthlyClose>(),
                Active<MonthlyCloseParticipant>(),
                Active<DistributionPayment>(),
                Active<Chair>(),
                Active<ActivityRecord>(),
                Active<UnofficialExpense>(),
                Active<CollaboratorContribution>(),
                Active<CollaboratorContributionEvent>(),
                Active<FinancialReserve>(),
                Active<FinancialCloseExclusion>(),
                Active<MonthlyPurchaseItem>(),
                Active<Loan>(),
                Active<LoanInstallment>(),
                Active<LoanPayment>(),
                Active<AnnualClose>(),
                Active<AnnualCarryover>()));

        public Task SaveAsync(
            IReadOnlyCollection<AuditableEntity> additions,
            IReadOnlyCollection<AuditableEntity> updates,
            CancellationToken cancellationToken = default)
        {
            entities.AddRange(additions);
            return Task.CompletedTask;
        }

        public Task SaveCompletingDraftAsync(
            IReadOnlyCollection<AuditableEntity> additions,
            IReadOnlyCollection<AuditableEntity> updates,
            string completedDraftKey,
            CancellationToken cancellationToken = default) =>
            SaveAsync(additions, updates, cancellationToken);

        public Task SaveSettingsAndRateAsync(
            GeneralSettings settings,
            WeeklyRate? newRate,
            CancellationToken cancellationToken = default)
        {
            if (newRate is not null)
            {
                entities.Add(newRate);
            }
            return Task.CompletedTask;
        }

        public Task SaveSettingsAndRateCompletingDraftAsync(
            GeneralSettings settings,
            WeeklyRate? newRate,
            string completedDraftKey,
            CancellationToken cancellationToken = default) =>
            SaveSettingsAndRateAsync(settings, newRate, cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
