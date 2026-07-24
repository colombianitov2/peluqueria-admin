using PeluqueriaAdmin.App.ViewModels;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Drafts;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Drafts;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.Tests;

public sealed class Phase412InventoryWorkflowTests
{
    private static readonly DateTime Utc =
        new(2026, 7, 24, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task InventoryCurrent_AddsOnlyAListedProductAndEditsTheLinkedPlanProductAndPurchase()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(Utc));
        var clock = new FixedTimeProvider(new DateTimeOffset(Utc));
        var drafts = new FakeFormDraftStore();
        var service = new AdministrationService(repository, settingsRepository, clock);
        MonthlyPurchaseItem plan = MonthlyPurchaseItem.Create(
            "Champú",
            ProductCategory.OtherProductForSale,
            new YearMonth(2026, 7),
            5,
            Money.FromDecimal(10m),
            true,
            false,
            Utc,
            "Producto deseado");
        await service.AddMonthlyPurchaseItemAsync(plan, cancellationToken);
        var editor = new AdministrationViewModel(
            service,
            new GetSettingsUseCase(settingsRepository),
            drafts,
            clock);
        var viewModel = new InventoryViewModel(
            editor,
            service,
            new GetSettingsUseCase(settingsRepository),
            clock,
            drafts);

        await viewModel.LoadAsync();

        Assert.Empty(viewModel.CurrentInventory);
        MonthlyPurchaseRow pending = Assert.Single(viewModel.PendingMonthlyPurchaseRows);
        viewModel.SelectedPendingMonthlyPurchaseRow = pending;
        editor.FormDate = new DateTime(2026, 7, 24);
        editor.QuantityText = "4";
        editor.AmountText = "18";
        editor.OptionalDescriptionText = "Presentación comprada";

        await viewModel.RegisterMonthlyPurchaseCommand.ExecuteAsync(null);

        InventoryCurrentRow current = Assert.Single(viewModel.CurrentInventory);
        Assert.Equal(plan.Id, current.Plan.Id);
        Assert.Equal("Champú", current.Name);
        Assert.Equal("5", current.ExpectedQuantity);
        Assert.Equal("4", current.PurchasedQuantity);
        Assert.Equal("2026-07-24", current.AddedDate);
        Assert.Contains("18", current.SalePrice, StringComparison.Ordinal);
        Assert.Equal("Presentación comprada", current.InventoryDescription);
        Assert.Empty(viewModel.PendingMonthlyPurchaseRows);

        viewModel.SelectedCurrentRow = current;
        viewModel.EditInventorySelectionCommand.Execute(null);
        viewModel.InventoryEditName = "Champú grande";
        viewModel.InventoryEditCategory = "Otro producto para venta";
        viewModel.InventoryEditExpectedQuantity = "6";
        viewModel.InventoryEditExpectedUnitCost = "11";
        viewModel.InventoryEditListDescription = "Producto deseado editado";
        viewModel.InventoryEditDate = new DateTime(2026, 7, 25);
        viewModel.InventoryEditPurchasedQuantity = "5";
        viewModel.InventoryEditSalePrice = "20";
        viewModel.InventoryEditDescription = "Presentación comprada editada";

        await viewModel.SaveInventorySelectionEditCommand.ExecuteAsync(null);

        AdministrationData data = await service.LoadAsync(cancellationToken);
        MonthlyPurchaseItem savedPlan = Assert.Single(data.MonthlyPurchaseItems);
        Product savedProduct = Assert.Single(data.Products);
        InventoryMovement savedPurchase = Assert.Single(data.InventoryMovements);
        Assert.Equal("Champú grande", savedPlan.Name);
        Assert.Equal(6m, savedPlan.Quantity);
        Assert.Equal(1_100, savedPlan.ExpectedUnitCost.MinorUnits);
        Assert.Equal("Producto deseado editado", savedPlan.Description);
        Assert.Equal("Champú grande", savedProduct.Name);
        Assert.Equal(2_000, savedProduct.DefaultSalePrice?.MinorUnits);
        Assert.Equal("Presentación comprada editada", savedProduct.Description);
        Assert.Equal(new DateOnly(2026, 7, 25), savedPurchase.Date);
        Assert.Equal(5m, savedPurchase.QuantityDelta);
        Assert.Equal(5_500, savedPurchase.CashAmount?.MinorUnits);
        Assert.Equal("Presentación comprada editada", savedPurchase.Description);
        Assert.Single(viewModel.CurrentInventory);
    }

    [Fact]
    public void PurchaseList_TotalIsQuantityTimesUnitOrPackagePrice()
    {
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(Utc));
        var clock = new FixedTimeProvider(new DateTimeOffset(Utc));
        var viewModel = new InventoryViewModel(
            new AdministrationViewModel(
                new AdministrationService(repository, settingsRepository, clock),
                new GetSettingsUseCase(settingsRepository),
                new FakeFormDraftStore(),
                clock),
            new AdministrationService(repository, settingsRepository, clock),
            new GetSettingsUseCase(settingsRepository),
            clock,
            new FakeFormDraftStore());

        viewModel.MonthlyPurchaseQuantity = "3";
        viewModel.MonthlyPurchaseUnitCost = "4.5";

        Assert.Contains("13", viewModel.MonthlyPurchaseExpectedTotalText, StringComparison.Ordinal);
        Assert.DoesNotContain("Mes", viewModel.MonthlyPurchaseExpectedTotalText, StringComparison.Ordinal);
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

        public Task<AdministrationData> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdministrationData(
                Active<PeluqueriaAdmin.Domain.LocalUse.LocalUsePerson>(),
                Active<PeluqueriaAdmin.Domain.LocalUse.WeeklyRate>(),
                Active<PeluqueriaAdmin.Domain.LocalUse.WeeklyCharge>(),
                Active<PeluqueriaAdmin.Domain.LocalUse.LocalUsePayment>(),
                Active<Product>(),
                Active<InventoryMovement>(),
                Active<MonthlyRestockPlan>(),
                Active<PeluqueriaAdmin.Domain.Finance.FinancialEntry>(),
                Active<PeluqueriaAdmin.Domain.Obligations.Obligation>(),
                Active<PeluqueriaAdmin.Domain.Obligations.ObligationPayment>(),
                Active<PeluqueriaAdmin.Domain.Maintenance.MaintenanceRecord>(),
                Active<PeluqueriaAdmin.Domain.Collaborators.Collaborator>(),
                Active<PeluqueriaAdmin.Domain.Collaborators.MonthlyClose>(),
                Active<PeluqueriaAdmin.Domain.Collaborators.MonthlyCloseParticipant>(),
                Active<PeluqueriaAdmin.Domain.Collaborators.DistributionPayment>(),
                Active<PeluqueriaAdmin.Domain.LocalUse.Chair>(),
                Active<PeluqueriaAdmin.Domain.Activity.ActivityRecord>(),
                Active<PeluqueriaAdmin.Domain.Finance.UnofficialExpense>(),
                Active<PeluqueriaAdmin.Domain.Collaborators.CollaboratorContribution>(),
                Active<PeluqueriaAdmin.Domain.Finance.FinancialReserve>(),
                Active<PeluqueriaAdmin.Domain.Finance.FinancialCloseExclusion>(),
                Active<MonthlyPurchaseItem>(),
                Active<PeluqueriaAdmin.Domain.Obligations.Loan>(),
                Active<PeluqueriaAdmin.Domain.Obligations.LoanPayment>(),
                Active<PeluqueriaAdmin.Domain.Finance.AnnualClose>()));

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
            PeluqueriaAdmin.Domain.LocalUse.WeeklyRate? newRate,
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
            PeluqueriaAdmin.Domain.LocalUse.WeeklyRate? newRate,
            string completedDraftKey,
            CancellationToken cancellationToken = default) =>
            SaveSettingsAndRateAsync(settings, newRate, cancellationToken);

        private T[] Active<T>() where T : AuditableEntity => entities
            .OfType<T>()
            .Where(item => !item.IsDeleted)
            .ToArray();
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
