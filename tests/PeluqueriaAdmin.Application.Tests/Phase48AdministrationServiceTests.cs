using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Activity;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Tests;

public sealed class Phase48AdministrationServiceTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly YearMonth July = new(2026, 7);

    [Fact]
    public async Task ManualClose_CreatesReserveAndFrozenCollaboratorSnapshot()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepository();
        AdministrationService service = CreateService(repository);
        Collaborator collaborator = Collaborator.Create("Socio", July.FirstDay, null, UtcNow);
        await service.AddAsync(collaborator, cancellationToken);
        await service.UpdateCollaboratorFundParticipationAsync(collaborator.Id, Percentage.FromPercent(100m), cancellationToken);
        await service.AddAsync(FinancialEntry.CreateIncome(July.FirstDay, "Ingreso", Money.FromDecimal(1_000m), UtcNow), cancellationToken);
        Obligation obligation = Obligation.Create("Electricidad", ObligationType.Service, July.LastDay,
            Money.FromDecimal(100m), RecurrenceFrequency.None, UtcNow);
        await service.AddAsync(obligation, cancellationToken);

        var closed = await service.CloseFinancialMonthAsync(July, cancellationToken);
        await service.UpdateCollaboratorFundParticipationAsync(collaborator.Id, Percentage.FromPercent(10m), cancellationToken);

        MonthlyCloseParticipant participant = Assert.Single(closed.Participants);
        Assert.Equal(2_000, participant.GlobalPercentageBasisPoints);
        Assert.Equal(10_000, participant.IndividualPercentageBasisPoints);
        Assert.Equal(18_000, participant.Amount.MinorUnits);
        FinancialReserve reserve = Assert.Single(repository.Active<FinancialReserve>());
        Assert.Equal(10_000, reserve.ReservedAmount.MinorUnits);
    }

    [Fact]
    public async Task DistributionPayment_RejectsArbitraryPartialValueAndAcceptsFullFrozenAmount()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepository();
        AdministrationService service = CreateService(repository);
        Collaborator collaborator = Collaborator.Create("Socio", July.FirstDay, null, UtcNow);
        await service.AddAsync(collaborator, cancellationToken);
        await service.UpdateCollaboratorFundParticipationAsync(collaborator.Id, Percentage.FromPercent(100m), cancellationToken);
        await service.AddAsync(FinancialEntry.CreateIncome(July.FirstDay, "Ingreso", Money.FromDecimal(100m), UtcNow), cancellationToken);
        MonthlyCloseParticipant participant = Assert.Single((await service.CloseFinancialMonthAsync(July, cancellationToken)).Participants);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterDistributionPaymentAsync(
            participant.Id, July.LastDay, Money.FromDecimal(10m), cancellationToken));
        DistributionPayment payment = await service.RegisterDistributionPaymentAsync(
            participant.Id, July.LastDay, participant.Amount, cancellationToken);

        Assert.Equal(2_000, payment.Amount.MinorUnits);
        Assert.Single(repository.Active<DistributionPayment>());
    }

    [Fact]
    public async Task Exclusion_RequiresReasonAndPreventsReserveWithoutDeletingCommitment()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepository();
        AdministrationService service = CreateService(repository);
        Obligation obligation = Obligation.Create("Impuesto", ObligationType.Tax, July.LastDay,
            Money.FromDecimal(100m), RecurrenceFrequency.None, UtcNow);
        await service.AddAsync(obligation, cancellationToken);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SetCloseExclusionAsync(
            July, FinancialCommitmentSource.Obligation, obligation.Id, true, " ", cancellationToken));
        await service.SetCloseExclusionAsync(July, FinancialCommitmentSource.Obligation,
            obligation.Id, true, "Importe pendiente de confirmar", cancellationToken);
        await service.CloseFinancialMonthAsync(July, cancellationToken);

        Assert.Single(repository.Active<Obligation>());
        Assert.Single(repository.Active<FinancialCloseExclusion>());
        Assert.Empty(repository.Active<FinancialReserve>());
    }

    [Fact]
    public async Task MaintenanceWithoutCost_BlocksCloseUntilAuditedExclusionIsSaved()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepository();
        AdministrationService service = CreateService(repository);
        MaintenanceRecord maintenance = MaintenanceRecord.Schedule("Aire", "Sin estimación", July.LastDay,
            null, MaintenanceFrequency.Once, null, null, UtcNow);
        await service.AddAsync(maintenance, cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseFinancialMonthAsync(July, cancellationToken));
        await service.SetCloseExclusionAsync(July, FinancialCommitmentSource.Maintenance, maintenance.Id,
            true, "El importe se confirmará después", cancellationToken);
        await service.CloseFinancialMonthAsync(July, cancellationToken);

        Assert.Single(repository.Active<MonthlyClose>(), item => item.IsConfirmed);
        Assert.Empty(repository.Active<FinancialReserve>());
    }

    [Fact]
    public async Task ReopenAndCloseAgain_LeavesOneActiveReserveAndOneActiveAssignment()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepository();
        AdministrationService service = CreateService(repository);
        Collaborator collaborator = Collaborator.Create("Socio", July.FirstDay, null, UtcNow);
        await service.AddAsync(collaborator, cancellationToken);
        await service.UpdateCollaboratorFundParticipationAsync(collaborator.Id, Percentage.FromPercent(100m), cancellationToken);
        Obligation obligation = Obligation.Create("Servicio", ObligationType.Service, July.LastDay,
            Money.FromDecimal(50m), RecurrenceFrequency.None, UtcNow);
        await service.AddAsync(obligation, cancellationToken);
        await service.AddAsync(FinancialEntry.CreateIncome(July.FirstDay, "Ingreso", Money.FromDecimal(100m), UtcNow), cancellationToken);

        MonthlyClose first = (await service.CloseFinancialMonthAsync(July, cancellationToken)).Close;
        await service.ReopenMonthAsync(first.Id, cancellationToken);
        await service.CloseFinancialMonthAsync(July, cancellationToken);

        Assert.Single(repository.Active<MonthlyClose>(), item => item.IsConfirmed);
        Assert.Single(repository.Active<MonthlyCloseParticipant>());
        Assert.Single(repository.Active<FinancialReserve>());
        Assert.Equal(2, repository.Entities.OfType<MonthlyClose>().Count());
        Assert.Equal(2, repository.Entities.OfType<FinancialReserve>().Count());
    }

    [Fact]
    public async Task PurchaseAfterClose_ConsumesPriorMonthlyReserveAndOnlyDifferenceAffectsLaterMonth()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepository();
        AdministrationService service = CreateService(repository);
        Product product = Product.Create("Aseo", ProductCategory.Cleaning, "unidad", UtcNow);
        await service.AddProductAsync(product, cancellationToken);
        MonthlyPurchaseItem item = MonthlyPurchaseItem.Create(product.Id, July, 2,
            Money.FromDecimal(50m), true, true, UtcNow);
        await service.AddMonthlyPurchaseItemAsync(item, cancellationToken);
        await service.CloseFinancialMonthAsync(July, cancellationToken);

        InventoryMovement purchase = await service.RegisterPurchaseAsync(product.Id,
            new DateOnly(2026, 8, 2), Quantity.Positive(2), Money.FromDecimal(55m), cancellationToken: cancellationToken);
        FinancialMonthSnapshot august = await service.CalculateFinancialMonthAsync(new YearMonth(2026, 8), cancellationToken);

        Assert.Equal(purchase.Id, item.PurchaseMovementId);
        FinancialReserve reserve = Assert.Single(repository.Active<FinancialReserve>());
        Assert.True(reserve.IsConsumed);
        Assert.Equal(11_000, reserve.ActualAmount?.MinorUnits);
        Assert.Equal(1_000, august.ReserveAdjustmentsMinorUnits);
        Assert.Equal(-1_000, august.DistributableResultMinorUnits);
    }

    [Fact]
    public async Task ManualPurchaseLink_ConsumesTheSameReserveAsAutomaticLink()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepository();
        AdministrationService service = CreateService(repository);
        Product product = Product.Create("Aseo", ProductCategory.Cleaning, "unidad", UtcNow);
        await service.AddProductAsync(product, cancellationToken);
        MonthlyPurchaseItem item = MonthlyPurchaseItem.Create(product.Id, July, 2,
            Money.FromDecimal(50m), true, true, UtcNow);
        await service.AddMonthlyPurchaseItemAsync(item, cancellationToken);
        await service.CloseFinancialMonthAsync(July, cancellationToken);
        InventoryMovement purchase = InventoryMovement.Purchase(product.Id, new DateOnly(2026, 8, 2),
            Quantity.Positive(2), Money.FromDecimal(110m), UtcNow);
        await service.AddInventoryMovementAsync(purchase, cancellationToken);

        await service.LinkMonthlyPurchaseAsync(item.Id, purchase.Id, cancellationToken);

        Assert.Equal(purchase.Id, item.PurchaseMovementId);
        FinancialReserve reserve = Assert.Single(repository.Active<FinancialReserve>());
        Assert.True(reserve.IsConsumed);
        Assert.Equal(11_000, reserve.ActualAmount?.MinorUnits);
    }

    [Fact]
    public async Task MonthlyPurchase_CreatesProductAndPurchaseAtomically_UsingPlannedCostAndEnteredSalePrice()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepository();
        AdministrationService service = CreateService(repository);
        MonthlyPurchaseItem plan = MonthlyPurchaseItem.Create(
            "Tinte cobre",
            ProductCategory.OtherProductForSale,
            July,
            2,
            Money.FromDecimal(7.25m),
            true,
            false,
            UtcNow,
            "Producto planificado");
        await service.AddMonthlyPurchaseItemAsync(plan, cancellationToken);

        InventoryMovement purchase = await service.RegisterMonthlyPurchaseAsync(
            plan.Id,
            new DateOnly(2026, 7, 23),
            Quantity.Positive(3),
            Money.FromDecimal(20m),
            "Compra real",
            cancellationToken,
            "Inventario:Registrar compra:new");

        Product product = Assert.Single(repository.Active<Product>());
        Assert.Equal("Tinte cobre", product.Name);
        Assert.Equal(2_000, product.DefaultSalePrice?.MinorUnits);
        Assert.Equal(725, product.DefaultUnitCost?.MinorUnits);
        Assert.Equal(InventoryMovementType.Purchase, purchase.Type);
        Assert.Equal(3m, purchase.QuantityDelta);
        Assert.Equal(2_175, purchase.CashAmount?.MinorUnits);
        Assert.Equal("Compra real", purchase.Description);
        Assert.Equal(product.Id, plan.ProductId);
        Assert.Equal(purchase.Id, plan.PurchaseMovementId);
        Assert.Contains("Inventario:Registrar compra:new", repository.CompletedDraftKeys);
        Assert.DoesNotContain(repository.Active<InventoryMovement>(),
            item => item.Type == InventoryMovementType.InitialStock);
    }

    [Fact]
    public async Task MonthlyPurchase_UsesTheExactSelectedExistingProduct_UpdatesSalePriceAndCannotRepeat()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepository();
        AdministrationService service = CreateService(repository);
        Product product = Product.Create(
            "Cera",
            ProductCategory.OtherProductForSale,
            "unidad",
            UtcNow,
            Money.FromDecimal(10m),
            defaultUnitCost: Money.FromDecimal(5m));
        await service.AddProductAsync(product, cancellationToken);
        MonthlyPurchaseItem plan = MonthlyPurchaseItem.Create(
            product.Id,
            July,
            1,
            Money.FromDecimal(8m),
            false,
            true,
            UtcNow,
            "Reposición seleccionada");
        await service.AddMonthlyPurchaseItemAsync(plan, cancellationToken);

        InventoryMovement purchase = await service.RegisterMonthlyPurchaseAsync(
            plan.Id,
            new DateOnly(2026, 7, 24),
            Quantity.Positive(2),
            Money.FromDecimal(14m),
            cancellationToken: cancellationToken);

        Assert.Single(repository.Active<Product>());
        Assert.Equal(product.Id, purchase.ProductId);
        Assert.Equal(1_400, product.DefaultSalePrice?.MinorUnits);
        Assert.Equal(800, product.DefaultUnitCost?.MinorUnits);
        Assert.Equal(1_600, purchase.CashAmount?.MinorUnits);
        Assert.Equal(purchase.Id, plan.PurchaseMovementId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterMonthlyPurchaseAsync(
            plan.Id,
            new DateOnly(2026, 7, 25),
            Quantity.Positive(1),
            Money.FromDecimal(15m),
            cancellationToken: cancellationToken));
        Assert.Single(repository.Active<InventoryMovement>());
    }

    [Fact]
    public async Task MonthlyPurchase_SettlesItsExactReserveWithPlannedUnitCostTimesActualQuantity()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepository();
        AdministrationService service = CreateService(repository);
        MonthlyPurchaseItem plan = MonthlyPurchaseItem.Create(
            "Champú de uso interno",
            ProductCategory.LocalSupply,
            July,
            2,
            Money.FromDecimal(12.50m),
            true,
            false,
            UtcNow);
        await service.AddMonthlyPurchaseItemAsync(plan, cancellationToken);
        FinancialReserve reserve = FinancialReserve.Create(
            July,
            FinancialCommitmentSource.MonthlyPurchase,
            plan.Id,
            plan.Name,
            July.LastDay,
            Money.FromDecimal(25m),
            UtcNow);
        repository.Entities.Add(reserve);

        InventoryMovement purchase = await service.RegisterMonthlyPurchaseAsync(
            plan.Id,
            new DateOnly(2026, 7, 26),
            Quantity.Positive(3),
            null,
            cancellationToken: cancellationToken);

        Assert.True(reserve.IsConsumed);
        Assert.Equal(new DateOnly(2026, 7, 26), reserve.SettledDate);
        Assert.Equal(3_750, reserve.ActualAmount?.MinorUnits);
        Assert.Equal(3_750, purchase.CashAmount?.MinorUnits);
        Assert.Null(Assert.Single(repository.Active<Product>()).DefaultSalePrice);
    }

    [Fact]
    public async Task MonthlyPurchase_ReplacesUnavailableDeletedProductLinkAndKeepsThePlanUsable()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepository();
        AdministrationService service = CreateService(repository);
        Product deletedProduct = Product.Create(
            "Cera eliminada",
            ProductCategory.OtherProductForSale,
            "unidad",
            UtcNow,
            Money.FromDecimal(11m),
            defaultUnitCost: Money.FromDecimal(6m));
        await service.AddProductAsync(deletedProduct, cancellationToken);
        MonthlyPurchaseItem plan = MonthlyPurchaseItem.Create(
            "Cera eliminada",
            ProductCategory.OtherProductForSale,
            July,
            2,
            Money.FromDecimal(7m),
            true,
            false,
            UtcNow,
            "Plan que conserva su uso",
            deletedProduct.Id);
        await service.AddMonthlyPurchaseItemAsync(plan, cancellationToken);
        deletedProduct.MarkDeleted(UtcNow.AddMinutes(1));

        InventoryMovement purchase = await service.RegisterMonthlyPurchaseAsync(
            plan.Id,
            new DateOnly(2026, 7, 27),
            Quantity.Positive(3),
            Money.FromDecimal(12m),
            cancellationToken: cancellationToken);

        Product replacement = Assert.Single(repository.Active<Product>());
        Assert.NotEqual(deletedProduct.Id, replacement.Id);
        Assert.Equal("Cera eliminada", replacement.Name);
        Assert.Equal(replacement.Id, plan.ProductId);
        Assert.Equal(replacement.Id, purchase.ProductId);
        Assert.Equal(purchase.Id, plan.PurchaseMovementId);
    }

    [Fact]
    public async Task CloseYear_RequiresTwelveMonths_PreservesThem_AndRejectsDuplicateAnnualClose()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeRepository();
        AdministrationService service = CreateService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseYearAsync(2026, cancellationToken));
        for (int month = 1; month <= 12; month++)
        {
            await service.CloseFinancialMonthAsync(new YearMonth(2026, month), cancellationToken);
        }

        AnnualClose annual = await service.CloseYearAsync(2026, cancellationToken);

        Assert.Equal(2026, annual.Year);
        Assert.Equal(12, repository.Active<MonthlyClose>().Count);
        Assert.Single(repository.Active<AnnualClose>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseYearAsync(2026, cancellationToken));
    }

    private static AdministrationService CreateService(FakeRepository repository) => new(
        repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)),
        new FixedTimeProvider(new DateTimeOffset(UtcNow)));

    private sealed class FakeSettingsRepository(GeneralSettings settings) : ISettingsRepository
    {
        public Task<GeneralSettings> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(settings);
        public Task SaveAsync(GeneralSettings value, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeRepository : IAdministrationRepository
    {
        public List<AuditableEntity> Entities { get; } = [];
        public List<string> CompletedDraftKeys { get; } = [];
        public IReadOnlyList<T> Active<T>() where T : AuditableEntity => Entities.OfType<T>().Where(item => !item.IsDeleted).ToArray();

        public Task<AdministrationData> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AdministrationData(
            Active<LocalUsePerson>(), Active<WeeklyRate>(), Active<WeeklyCharge>(), Active<LocalUsePayment>(),
            Active<Product>(), Active<InventoryMovement>(), Active<MonthlyRestockPlan>(), Active<FinancialEntry>(),
            Active<Obligation>(), Active<ObligationPayment>(), Active<MaintenanceRecord>(), Active<Collaborator>(),
            Active<MonthlyClose>(), Active<MonthlyCloseParticipant>(), Active<DistributionPayment>(), Active<Chair>(),
            Active<ActivityRecord>(), Active<UnofficialExpense>(), Active<CollaboratorContribution>(), Active<FinancialReserve>(),
            Active<FinancialCloseExclusion>(), Active<MonthlyPurchaseItem>(), Active<Loan>(), Active<LoanPayment>(), Active<AnnualClose>()));

        public Task SaveAsync(IReadOnlyCollection<AuditableEntity> additions,
            IReadOnlyCollection<AuditableEntity> updates, CancellationToken cancellationToken = default)
        {
            Entities.AddRange(additions);
            return Task.CompletedTask;
        }

        public Task SaveCompletingDraftAsync(IReadOnlyCollection<AuditableEntity> additions,
            IReadOnlyCollection<AuditableEntity> updates, string completedDraftKey,
            CancellationToken cancellationToken = default)
        {
            CompletedDraftKeys.Add(completedDraftKey);
            return SaveAsync(additions, updates, cancellationToken);
        }

        public Task SaveSettingsAndRateAsync(GeneralSettings settings, WeeklyRate? newRate,
            CancellationToken cancellationToken = default)
        {
            if (newRate is not null) Entities.Add(newRate);
            return Task.CompletedTask;
        }

        public Task SaveSettingsAndRateCompletingDraftAsync(GeneralSettings settings, WeeklyRate? newRate,
            string completedDraftKey, CancellationToken cancellationToken = default) =>
            SaveSettingsAndRateAsync(settings, newRate, cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
