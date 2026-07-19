using OxyPlot.Series;
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
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.Tests;

public sealed class AdministrationViewModelTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task NewRecordDraft_IsPersistedAndRecoveredByANewViewModel()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var drafts = new FakeFormDraftStore();
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        var first = new AdministrationViewModel(service, new GetSettingsUseCase(settingsRepository), drafts, timeProvider);
        await first.SelectModuleAsync(AdministrationViewModel.OtherIncomeModule);

        first.PrimaryText = "+Concepto aún incompleto";
        await first.FlushPendingAsync();

        var second = new AdministrationViewModel(service, new GetSettingsUseCase(settingsRepository), drafts, timeProvider);
        await second.SelectModuleAsync(AdministrationViewModel.OtherIncomeModule);
        Assert.Equal("+Concepto aún incompleto", second.PrimaryText);
        Assert.True(second.HasRecoveredDraft);
        Assert.Empty((await service.LoadAsync(cancellationToken)).FinancialEntries);
    }

    [Fact]
    public async Task LoadedEdit_IsAutosavedAfterDebounceWithoutDeleteConfirmation()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        var viewModel = new AdministrationViewModel(service, new GetSettingsUseCase(settingsRepository), new FakeFormDraftStore(), timeProvider);
        Collaborator collaborator = Collaborator.Create("Original", new DateOnly(2026, 7, 1), null, UtcNow);
        await service.AddAsync(collaborator, cancellationToken);
        await viewModel.SelectModuleAsync(AdministrationViewModel.CollaboratorsModule);
        viewModel.SelectedRow = viewModel.Rows.Single(x => x.Entity?.Id == collaborator.Id);
        viewModel.LoadSelectedCommand.Execute(null);

        viewModel.PrimaryText = "Guardado automático";
        await Task.Delay(900, TestContext.Current.CancellationToken);

        Assert.Equal("Guardado automático", collaborator.Name);
        Assert.Contains("automáticamente", viewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.False(viewModel.ConfirmDelete);
    }

    [Fact]
    public async Task EditDoesNotRequireDeleteConfirmationAndDeleteDoes()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        var viewModel = new AdministrationViewModel(
            service,
            new GetSettingsUseCase(settingsRepository),
            new FakeFormDraftStore(),
            timeProvider);
        Collaborator collaborator = Collaborator.Create(
            "Ana", new DateOnly(2026, 7, 1), null, UtcNow);
        await service.AddAsync(collaborator, cancellationToken);
        await viewModel.SelectModuleAsync(AdministrationViewModel.CollaboratorsModule);
        viewModel.SelectedRow = viewModel.Rows.Single(item => item.Entity?.Id == collaborator.Id);
        viewModel.LoadSelectedCommand.Execute(null);
        viewModel.PrimaryText = "Ana editada";

        await viewModel.SaveEditCommand.ExecuteAsync(null);

        Assert.Equal("Ana editada", collaborator.Name);
        Assert.False(viewModel.IsError);
        viewModel.SelectedRow = viewModel.Rows.Single(item => item.Entity?.Id == collaborator.Id);

        await viewModel.DeleteCommand.ExecuteAsync(null);

        Assert.False(collaborator.IsDeleted);
        Assert.Contains("Confirmo eliminar", viewModel.StatusMessage, StringComparison.Ordinal);

        viewModel.ConfirmDelete = true;
        await viewModel.DeleteCommand.ExecuteAsync(null);

        Assert.True(collaborator.IsDeleted);
        Assert.False(viewModel.ConfirmDelete);
    }

    [Fact]
    public async Task LegacyPaymentWithDeletedObligation_IsDisplayedWithoutCrashing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        var viewModel = new AdministrationViewModel(
            service,
            new GetSettingsUseCase(settingsRepository),
            new FakeFormDraftStore(),
            timeProvider);
        Obligation obligation = Obligation.Create(
            "Impuesto histórico",
            ObligationType.Tax,
            new DateOnly(2026, 7, 1),
            Money.FromDecimal(20m),
            RecurrenceFrequency.None,
            UtcNow);
        await service.AddAsync(obligation, cancellationToken);
        await service.AddAsync(ObligationPayment.Create(
            obligation.Id,
            new DateOnly(2026, 7, 2),
            Money.FromDecimal(5m),
            UtcNow), cancellationToken);
        obligation.MarkDeleted(UtcNow.AddMinutes(1));

        await viewModel.SelectModuleAsync(AdministrationViewModel.ObligationsModule);

        OperationRow payment = Assert.Single(viewModel.Rows);
        Assert.Equal("Obligación eliminada", payment.Principal);
        Assert.False(viewModel.IsError);
    }

    [Fact]
    public async Task SpanishLabels_RoundTripWhenEditingSupportedCategories()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        var viewModel = new AdministrationViewModel(
            service,
            new GetSettingsUseCase(settingsRepository),
            new FakeFormDraftStore(),
            timeProvider);
        Product product = Product.Create(
            "Secador", ProductCategory.DurableEquipment, "unidad", UtcNow);
        FinancialEntry expense = FinancialEntry.CreateExpense(
            new DateOnly(2026, 7, 1), "Gasto", ExpenseCategory.Other, Money.FromDecimal(5m), UtcNow);
        Obligation obligation = Obligation.Create(
            "Permiso", ObligationType.OtherRecurring, new DateOnly(2026, 7, 1),
            Money.FromDecimal(10m), RecurrenceFrequency.None, UtcNow);
        await service.AddProductAsync(product, cancellationToken);
        await service.AddAsync(expense, cancellationToken);
        await service.AddAsync(obligation, cancellationToken);

        await AssertEditRoundTripAsync(
            viewModel, AdministrationViewModel.InventoryModule, product, "Otro producto del local");
        await AssertEditRoundTripAsync(
            viewModel, AdministrationViewModel.ExpensesModule, expense, "Otro gasto");
        await AssertEditRoundTripAsync(
            viewModel, AdministrationViewModel.ObligationsModule, obligation, "Otra obligación");
    }

    [Fact]
    public async Task NewlyAddedHairdresser_IsImmediatelyAvailableInPaymentSelector()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        Chair chair = Chair.Create("Silla 1", new DateOnly(2026, 7, 18), null, UtcNow);
        await service.AddChairAsync(chair, cancellationToken);
        LocalUsePerson person = LocalUsePerson.Create("Juan", new DateOnly(2026, 7, 18), null, UtcNow);
        await service.AddLocalUsePersonWithChairAsync(person, chair.Id, new DateOnly(2026, 7, 18), cancellationToken);
        var viewModel = new AdministrationViewModel(
            service, new GetSettingsUseCase(settingsRepository), new FakeFormDraftStore(), timeProvider);

        await viewModel.SelectModuleAsync(AdministrationViewModel.LocalUseModule);
        await viewModel.SelectActionCommand.ExecuteAsync("Registrar pago");

        Assert.Contains(viewModel.EntityOptions, option => option.Id == person.Id && option.Display == "Juan");
    }

    [Fact]
    public async Task MonthlyCharts_UseTheSameMonthlySummaryValuesAndHaveSpanishSeries()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        await service.AddAsync(FinancialEntry.CreateIncome(
            new DateOnly(2026, 7, 2), "Ingreso", Money.FromDecimal(100m), UtcNow), cancellationToken);
        var viewModel = new AdministrationViewModel(
            service, new GetSettingsUseCase(settingsRepository), new FakeFormDraftStore(), timeProvider);

        await viewModel.SelectModuleAsync(AdministrationViewModel.MonthlySummaryModule);
        viewModel.DateText = "2026-07-01";
        await viewModel.RefreshCommand.ExecuteAsync(null);

        BarSeries bars = Assert.IsType<BarSeries>(Assert.Single(viewModel.IncomeGoalChart.Series));
        Assert.Equal(100d, bars.Items[0].Value);
        Assert.NotEmpty(viewModel.ExpenseCompositionChart.Series);
        LineSeries line = Assert.IsType<LineSeries>(Assert.Single(viewModel.ResultEvolutionChart.Series));
        Assert.Equal(12, line.Points.Count);
        Assert.Equal("Resultado retenido", line.Title);
    }

    [Fact]
    public async Task ActivityDefaultsToTodayAndChangesDayWithoutDeletingPreviousHistory()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new MutableTimeProvider(new DateTimeOffset(UtcNow));
        await repository.SaveAsync(
            [
                ActivityRecord.Create(new DateOnly(2026, 7, 17), AdministrationViewModel.LocalUseModule, "Alta", "Ayer", null, null, UtcNow.AddDays(-1)),
                ActivityRecord.Create(new DateOnly(2026, 7, 18), AdministrationViewModel.LocalUseModule, "Alta", "Hoy", null, null, UtcNow),
            ], [], cancellationToken);
        var viewModel = new AdministrationViewModel(
            new AdministrationService(repository, settingsRepository, timeProvider),
            new GetSettingsUseCase(settingsRepository), new FakeFormDraftStore(), timeProvider);

        await viewModel.SelectModuleAsync(AdministrationViewModel.LocalUseModule);
        Assert.Equal("Hoy", viewModel.SelectedPeriod);
        Assert.Equal("Hoy", Assert.Single(viewModel.ActivityRows).Principal);

        timeProvider.Now = new DateTimeOffset(UtcNow.AddDays(1));
        await repository.SaveAsync(
            [ActivityRecord.Create(new DateOnly(2026, 7, 19), AdministrationViewModel.LocalUseModule, "Alta", "Nuevo día", null, null, UtcNow.AddDays(1))],
            [], cancellationToken);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("Nuevo día", Assert.Single(viewModel.ActivityRows).Principal);
        Assert.Equal(3, (await repository.LoadAsync(cancellationToken)).ActivityRecords.Count);
    }

    private static async Task AssertEditRoundTripAsync(
        AdministrationViewModel viewModel,
        string module,
        AuditableEntity entity,
        string expectedLabel)
    {
        await viewModel.SelectModuleAsync(module);
        viewModel.SelectedRow = viewModel.Rows.Single(item => item.Entity?.Id == entity.Id);
        viewModel.LoadSelectedCommand.Execute(null);
        Assert.Equal(expectedLabel, viewModel.SecondaryText);

        await viewModel.SaveEditCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsError, viewModel.StatusMessage);
    }

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
        public Task<IReadOnlyList<FormDraft>> LoadAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FormDraft>>(drafts.Values.ToArray());
        public Task<FormDraft?> FindAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(drafts.GetValueOrDefault(key));
        public Task UpsertAsync(FormDraft draft, CancellationToken cancellationToken = default) { drafts[draft.Key] = draft; return Task.CompletedTask; }
        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) { drafts.Remove(key); return Task.CompletedTask; }
    }

    private sealed class FakeAdministrationRepository : IAdministrationRepository
    {
        private readonly List<AuditableEntity> entities = [];

        public Task<AdministrationData> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdministrationData(
                Active<LocalUsePerson>(), Active<WeeklyRate>(), Active<WeeklyCharge>(), Active<LocalUsePayment>(),
                Active<Product>(), Active<InventoryMovement>(), Active<MonthlyRestockPlan>(), Active<FinancialEntry>(),
                Active<Obligation>(), Active<ObligationPayment>(), Active<MaintenanceRecord>(), Active<Collaborator>(),
                Active<MonthlyClose>(), Active<MonthlyCloseParticipant>(), Active<DistributionPayment>(),
                Active<Chair>(), Active<PeluqueriaAdmin.Domain.Activity.ActivityRecord>(), Active<UnofficialExpense>()));

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
            CancellationToken cancellationToken = default) => SaveAsync(additions, updates, cancellationToken);

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

        private T[] Active<T>() where T : AuditableEntity => entities.OfType<T>()
            .Where(item => !item.IsDeleted)
            .ToArray();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
