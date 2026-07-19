using PeluqueriaAdmin.App.ViewModels;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
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
            viewModel, AdministrationViewModel.InventoryModule, product, "Equipo o bien duradero");
        await AssertEditRoundTripAsync(
            viewModel, AdministrationViewModel.ExpensesModule, expense, "Otro gasto");
        await AssertEditRoundTripAsync(
            viewModel, AdministrationViewModel.ObligationsModule, obligation, "Otra obligación");
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

    private sealed class FakeAdministrationRepository : IAdministrationRepository
    {
        private readonly List<AuditableEntity> entities = [];

        public Task<AdministrationData> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdministrationData(
                Active<LocalUsePerson>(), Active<WeeklyRate>(), Active<WeeklyCharge>(), Active<LocalUsePayment>(),
                Active<Product>(), Active<InventoryMovement>(), Active<MonthlyRestockPlan>(), Active<FinancialEntry>(),
                Active<Obligation>(), Active<ObligationPayment>(), Active<MaintenanceRecord>(), Active<Collaborator>(),
                Active<MonthlyClose>(), Active<MonthlyCloseParticipant>(), Active<DistributionPayment>()));

        public Task SaveAsync(
            IReadOnlyCollection<AuditableEntity> additions,
            IReadOnlyCollection<AuditableEntity> updates,
            CancellationToken cancellationToken = default)
        {
            entities.AddRange(additions);
            return Task.CompletedTask;
        }

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

        private T[] Active<T>() where T : AuditableEntity => entities.OfType<T>()
            .Where(item => !item.IsDeleted)
            .ToArray();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
