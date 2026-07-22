using System.Text.Json;
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
    public async Task AnnualBalance_InitializesTheVisibleQueryAndDataToTheCurrentYear()
    {
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var viewModel = new AdministrationViewModel(
            new AdministrationService(repository, settingsRepository, timeProvider),
            new GetSettingsUseCase(settingsRepository),
            new FakeFormDraftStore(),
            timeProvider);

        await viewModel.SelectModuleAsync(AdministrationViewModel.AnnualBalanceModule);

        Assert.Equal("2026", viewModel.SpecificYearText);
        Assert.Equal("2026-01-01", viewModel.DateText);
        Assert.False(viewModel.HasRecoveredDraft);
    }

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
        Assert.StartsWith("USD ", payment.Amount, StringComparison.Ordinal);
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
    public async Task MonthlyCharts_UseSelectedPeriodAndHaveSpanishSeries()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        await service.AddAsync(FinancialEntry.CreateIncome(
            new DateOnly(2026, 7, 2), "Ingreso", Money.FromDecimal(100m), UtcNow), cancellationToken);
        await service.AddAsync(FinancialEntry.CreateExpense(
            new DateOnly(2026, 7, 3), "Gasto", ExpenseCategory.Other,
            Money.FromDecimal(10m), UtcNow), cancellationToken);
        var viewModel = new AdministrationViewModel(
            service, new GetSettingsUseCase(settingsRepository), new FakeFormDraftStore(), timeProvider);

        await viewModel.SelectModuleAsync(AdministrationViewModel.MonthlySummaryModule);
        viewModel.SelectedPeriod = "Este mes";
        viewModel.DateText = "2026-07-01";
        await viewModel.RefreshCommand.ExecuteAsync(null);

        BarSeries bars = Assert.IsType<BarSeries>(Assert.Single(viewModel.IncomeGoalChart.Series));
        Assert.Equal(100d, bars.Items[0].Value);
        Assert.NotEmpty(viewModel.ExpenseCompositionChart.Series);
        LineSeries line = Assert.IsType<LineSeries>(Assert.Single(viewModel.ResultEvolutionChart.Series));
        Assert.Equal(31, line.Points.Count);
        Assert.Equal("Resultado retenido", line.Title);
    }

    [Theory]
    [InlineData("Hoy", 24)]
    [InlineData("Esta semana", 7)]
    [InlineData("Este mes", 31)]
    [InlineData("Últimos 3 meses", 3)]
    [InlineData("Últimos 6 meses", 6)]
    [InlineData("Este año", 12)]
    [InlineData("Fecha específica", 24)]
    [InlineData("Año específico", 12)]
    public async Task MonthlyCharts_AdaptHorizontalScaleWithControlledClock(string period, int expectedPoints)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        var viewModel = new AdministrationViewModel(
            service, new GetSettingsUseCase(settingsRepository), new FakeFormDraftStore(), timeProvider);
        await viewModel.SelectModuleAsync(AdministrationViewModel.MonthlySummaryModule);
        viewModel.SpecificDate = new DateTime(2026, 7, 2);
        viewModel.SpecificYearText = "2025";
        viewModel.SelectedPeriod = period;

        await viewModel.RefreshCommand.ExecuteAsync(null);

        LineSeries line = Assert.IsType<LineSeries>(Assert.Single(viewModel.ResultEvolutionChart.Series));
        Assert.Equal(expectedPoints, line.Points.Count);
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

    [Fact]
    public async Task LocalUse_ChairSelectorsRemainIndependentAcrossProfileAssignmentAndWithdrawal()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        DateOnly today = DateOnly.FromDateTime(UtcNow);
        Chair first = Chair.Create("Silla 1", today, null, UtcNow);
        Chair second = Chair.Create("Silla 2", today, null, UtcNow);
        await service.AddChairAsync(first, cancellationToken);
        await service.AddChairAsync(second, cancellationToken);
        var viewModel = new LocalUseViewModel(
            service, new GetSettingsUseCase(settingsRepository), new FakeFormDraftStore(), timeProvider);
        await viewModel.LoadAsync();

        viewModel.SelectedAction = "Añadir trabajador";
        viewModel.NameText = "Trabajador sin silla";
        viewModel.ActionDate = UtcNow;
        viewModel.SelectedNewWorkerChair = null;
        await viewModel.SaveActionCommand.ExecuteAsync(null);

        WorkerRow worker = Assert.Single(viewModel.Workers);
        Assert.Equal("Sin silla", worker.Chair);
        viewModel.NameText = "Borrador de otro trabajador";
        viewModel.SelectedNewWorkerChair = viewModel.NewWorkerChairOptions.Single(item => item.Id == second.Id);
        viewModel.SelectedWorkerRow = worker;
        await viewModel.OpenSelectedWorkerProfileCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.WorkerProfileChairOptions.Count);
        Assert.Equal(second.Id, viewModel.SelectedNewWorkerChair?.Id);
        Assert.Equal("Borrador de otro trabajador", viewModel.NameText);
        viewModel.WorkerProfileSelectedChair = viewModel.WorkerProfileChairOptions.Single(item => item.Id == first.Id);
        await viewModel.AssignProfileChairCommand.ExecuteAsync(null);

        Assert.Equal("Silla 1", viewModel.ProfileChair);
        Assert.Equal(second.Id, viewModel.SelectedNewWorkerChair?.Id);
        Assert.Contains("Silla asignada correctamente: Silla 1", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Equal(2, viewModel.WorkerProfileChairOptions.Count);

        await viewModel.UnassignProfileChairCommand.ExecuteAsync(null);

        Assert.Equal("Sin silla asignada", viewModel.ProfileChair);
        Assert.Equal(2, viewModel.WorkerProfileChairOptions.Count);
        Assert.All((await service.LoadAsync(cancellationToken)).Chairs, chair => Assert.Null(chair.AssignedPersonId));
        Assert.Single((await service.LoadAsync(cancellationToken)).LocalUsePeople);

        await viewModel.CloseWorkerProfileCommand.ExecuteAsync(null);
        Assert.Equal(second.Id, viewModel.SelectedNewWorkerChair?.Id);
        Assert.Equal("Borrador de otro trabajador", viewModel.NameText);
    }

    [Fact]
    public async Task LocalUse_AdvancePaymentUpdatesHistoryOnceAndCreditSurvivesRetirement()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        DateOnly today = DateOnly.FromDateTime(UtcNow);
        LocalUsePerson worker = LocalUsePerson.Create("Trabajador con deuda", today.AddDays(-14), null, UtcNow);
        await service.AddLocalUsePersonAsync(worker, today, cancellationToken);
        var viewModel = new LocalUseViewModel(
            service, new GetSettingsUseCase(settingsRepository), new FakeFormDraftStore(), timeProvider);
        await viewModel.LoadAsync();
        viewModel.SelectedPeriod = "Últimos 3 meses";
        viewModel.SelectedWorkerRow = viewModel.Workers.Single(item => item.Worker.Id == worker.Id);
        await viewModel.OpenSelectedWorkerProfileCommand.ExecuteAsync(null);
        await viewModel.RefreshCommand.ExecuteAsync(null);
        viewModel.PaymentDate = UtcNow;
        viewModel.PaymentAmount = "1000";
        viewModel.PaymentDescription = "Pago anticipado de prueba";

        await viewModel.RegisterWorkerPaymentCommand.ExecuteAsync(null);
        await viewModel.RegisterWorkerPaymentCommand.ExecuteAsync(null);

        AdministrationData paid = await service.LoadAsync(cancellationToken);
        Assert.Single(paid.LocalUsePayments);
        Assert.Contains("976", viewModel.ProfileCredit, StringComparison.Ordinal);
        Assert.Equal(string.Empty, viewModel.PaymentAmount);
        Assert.Equal(string.Empty, viewModel.PaymentDescription);
        Assert.Equal(UtcNow.Date, viewModel.PaymentDate?.Date);
        Assert.Single(viewModel.WorkerHistoryRows, item => item.Principal == "Pago registrado");

        viewModel.RetirementDate = UtcNow;
        await viewModel.RetireWorkerCommand.ExecuteAsync(null);

        Assert.Contains("976", viewModel.ProfileCredit, StringComparison.Ordinal);
        Assert.Contains("retirado", viewModel.ProfileNextRequiredPayment, StringComparison.OrdinalIgnoreCase);
        Assert.Single((await service.LoadAsync(cancellationToken)).LocalUsePayments);
    }

    [Fact]
    public async Task LocalUse_NewWorkerPersistsVisibleDateAndResetsDateWithoutLeakingPreviousSelection()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTime reviewUtc = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
        DateOnly today = new(2026, 7, 20);
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(reviewUtc));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(reviewUtc));
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        var viewModel = new LocalUseViewModel(
            service, new GetSettingsUseCase(settingsRepository), new FakeFormDraftStore(), timeProvider);
        await viewModel.LoadAsync();

        viewModel.ActionDate = new DateTime(2026, 6, 16);
        viewModel.SelectedAction = "Añadir trabajador";
        Assert.Equal(today.ToDateTime(TimeOnly.MinValue), viewModel.ActionDate);
        viewModel.NameText = "Ingreso actual";
        await viewModel.SaveActionCommand.ExecuteAsync(null);

        LocalUsePerson current = (await service.LoadAsync(cancellationToken)).LocalUsePeople.Single();
        Assert.Equal(today, current.EntryDate);
        Assert.Equal(0, WeeklyChargeCalculator.CalculateDebt(
            (await service.LoadAsync(cancellationToken)).WeeklyCharges.Where(item => item.PersonId == current.Id),
            [], today).MinorUnits);
        Assert.Equal(today.ToDateTime(TimeOnly.MinValue), viewModel.ActionDate);

        viewModel.NameText = "Ingreso histórico";
        viewModel.ActionDate = new DateTime(2026, 6, 16);
        await viewModel.SaveActionCommand.ExecuteAsync(null);

        AdministrationData data = await service.LoadAsync(cancellationToken);
        LocalUsePerson historical = data.LocalUsePeople.Single(item => item.Name == "Ingreso histórico");
        Assert.Equal(new DateOnly(2026, 6, 16), historical.EntryDate);
        Assert.Equal(4_800, WeeklyChargeCalculator.CalculateDebt(
            data.WeeklyCharges.Where(item => item.PersonId == historical.Id),
            data.LocalUsePayments.Where(item => item.PersonId == historical.Id),
            today).MinorUnits);
        Assert.Equal(today.ToDateTime(TimeOnly.MinValue), viewModel.ActionDate);
    }

    [Fact]
    public async Task LocalUse_RecoveredDraftMakesHistoricalDateExplicitBeforeSave()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTime reviewUtc = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(reviewUtc));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(reviewUtc));
        var drafts = new FakeFormDraftStore();
        string payload = JsonSerializer.Serialize(new
        {
            Action = "Añadir trabajador",
            Name = "Borrador visible",
            Date = (DateTime?)new DateTime(2026, 6, 16),
            Description = "Fecha histórica intencional",
            ChairId = (Guid?)null,
        });
        await drafts.UpsertAsync(FormDraft.Create(
            "Uso del local:Fase42:accion", "Uso del local", "Añadir trabajador",
            payload, null, false, reviewUtc), cancellationToken);
        var viewModel = new LocalUseViewModel(
            new AdministrationService(repository, settingsRepository, timeProvider),
            new GetSettingsUseCase(settingsRepository), drafts, timeProvider);

        await viewModel.LoadAsync();

        Assert.True(viewModel.HasRecoveredActionDraft);
        Assert.True(viewModel.IsWorkerAction);
        Assert.Equal("Borrador visible", viewModel.NameText);
        Assert.Equal(new DateTime(2026, 6, 16), viewModel.ActionDate);
    }

    [Fact]
    public async Task LocalUse_PaymentOutsidePreviousWeekAppearsImmediatelyOnceInCompleteHistory()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        DateTime reviewUtc = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);
        DateOnly today = new(2026, 7, 20);
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(reviewUtc));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(reviewUtc));
        var service = new AdministrationService(repository, settingsRepository, timeProvider);
        LocalUsePerson worker = LocalUsePerson.Create(
            "Trabajador con cuatro cuotas", new DateOnly(2026, 6, 16), null, reviewUtc);
        await service.AddLocalUsePersonAsync(worker, today, cancellationToken);
        var viewModel = new LocalUseViewModel(
            service, new GetSettingsUseCase(settingsRepository), new FakeFormDraftStore(), timeProvider);
        await viewModel.LoadAsync();
        viewModel.SelectedWorkerRow = viewModel.Workers.Single(item => item.Worker.Id == worker.Id);
        await viewModel.OpenSelectedWorkerProfileCommand.ExecuteAsync(null);
        Assert.Equal("Todo el historial", viewModel.SelectedWorkerHistoryPeriod);
        Assert.Contains("48", viewModel.ProfileDebt, StringComparison.Ordinal);
        viewModel.SelectedWorkerHistoryPeriod = "Esta semana";
        await viewModel.RefreshCommand.ExecuteAsync(null);
        Assert.Empty(viewModel.WorkerHistoryRows);
        viewModel.PaymentDate = new DateTime(2026, 7, 19);
        viewModel.PaymentAmount = "12";
        viewModel.PaymentDescription = "Pago del domingo";

        await viewModel.RegisterWorkerPaymentCommand.ExecuteAsync(null);

        Assert.Equal("Todo el historial", viewModel.SelectedWorkerHistoryPeriod);
        Assert.Contains("36", viewModel.ProfileDebt, StringComparison.Ordinal);
        Assert.Contains("2026-07-04", viewModel.ProfileNextRequiredPayment, StringComparison.Ordinal);
        Assert.Contains("12", viewModel.ProfileNextRequiredPayment, StringComparison.Ordinal);
        OperationRow payment = Assert.Single(
            viewModel.WorkerHistoryRows, item => item.Principal == "Pago registrado");
        Assert.Equal("2026-07-19", payment.Date);
        Assert.Equal("Pago del domingo", payment.Detail);
        Assert.Contains("12", payment.Amount, StringComparison.Ordinal);
        Assert.Equal("Pago en otra fecha", payment.Status);
        Assert.Single((await service.LoadAsync(cancellationToken)).LocalUsePayments);
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
