using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
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

public sealed class AdministrationServiceTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task GenerateScheduledRecords_IsAtomicAndIdempotent()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var service = CreateService(repository, settingsRepository);
        LocalUsePerson person = LocalUsePerson.Create("Ana", new DateOnly(2026, 7, 1), null, UtcNow);
        Obligation obligation = Obligation.Create(
            "Internet", ObligationType.Service, new DateOnly(2026, 7, 5),
            Money.FromDecimal(50m), RecurrenceFrequency.Monthly, UtcNow);
        await service.AddAsync(person, cancellationToken);
        await service.AddAsync(obligation, cancellationToken);

        AdministrationData first = await service.GenerateScheduledRecordsAsync(
            new DateOnly(2026, 8, 18), cancellationToken);
        AdministrationData second = await service.GenerateScheduledRecordsAsync(
            new DateOnly(2026, 8, 18), cancellationToken);

        Assert.Single(first.WeeklyRates);
        Assert.Equal(7, first.WeeklyCharges.Count);
        Assert.Equal(2, first.Obligations.Count);
        Assert.Equal(first.WeeklyCharges.Count, second.WeeklyCharges.Count);
        Assert.Equal(first.Obligations.Count, second.Obligations.Count);
        Assert.True(repository.LastSaveWasSingleTransaction);
    }

    [Fact]
    public async Task RegisterPayment_RejectsOverpaymentAtApplicationBoundary()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var service = CreateService(repository, settingsRepository);
        LocalUsePerson person = LocalUsePerson.Create("Luis", new DateOnly(2026, 7, 1), null, UtcNow);
        await service.AddAsync(person, cancellationToken);
        await service.GenerateScheduledRecordsAsync(new DateOnly(2026, 7, 1), cancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterLocalUsePaymentAsync(
            person.Id,
            new DateOnly(2026, 7, 2),
            Money.FromDecimal(13m),
            cancellationToken));
        Assert.Empty((await service.LoadAsync(cancellationToken)).LocalUsePayments);
    }

    [Fact]
    public async Task AddPerson_GeneratesChargesAndAllowsPaymentInSameSession()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        LocalUsePerson person = LocalUsePerson.Create("Ana", new DateOnly(2026, 7, 1), null, UtcNow);

        await service.AddLocalUsePersonAsync(person, new DateOnly(2026, 7, 18), cancellationToken);
        await service.RegisterLocalUsePaymentAsync(
            person.Id, new DateOnly(2026, 7, 18), Money.FromDecimal(12m), cancellationToken);

        AdministrationData data = await service.LoadAsync(cancellationToken);
        Assert.Equal(3, data.WeeklyCharges.Count);
        Assert.Single(data.LocalUsePayments);
        Assert.Equal(2_400, WeeklyChargeCalculator.CalculateDebt(
            data.WeeklyCharges, data.LocalUsePayments).MinorUnits);
    }

    [Fact]
    public async Task UpdatePerson_RejectsDateChangeThatInvalidatesPaidPeriods()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        LocalUsePerson person = LocalUsePerson.Create("Ana", new DateOnly(2026, 7, 1), null, UtcNow);
        await service.AddLocalUsePersonAsync(person, new DateOnly(2026, 7, 18), cancellationToken);
        await service.RegisterLocalUsePaymentAsync(
            person.Id, new DateOnly(2026, 7, 8), Money.FromDecimal(12m), cancellationToken);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateLocalUsePersonAsync(
                person.Id, "Ana", new DateOnly(2026, 7, 2), null,
                new DateOnly(2026, 7, 18), cancellationToken));

        Assert.Contains("ya tiene pagos", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AddRecurringObligation_GeneratesCurrentOccurrencesWithoutRestart()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        Obligation obligation = Obligation.Create(
            "Internet", ObligationType.Service, new DateOnly(2026, 5, 31),
            Money.FromDecimal(50m), RecurrenceFrequency.Monthly, UtcNow);

        await service.AddObligationAsync(obligation, new DateOnly(2026, 7, 31), cancellationToken);
        await service.GenerateScheduledRecordsAsync(new DateOnly(2026, 7, 31), cancellationToken);

        Assert.Equal(
            [new DateOnly(2026, 5, 31), new DateOnly(2026, 6, 30), new DateOnly(2026, 7, 31)],
            (await service.LoadAsync(cancellationToken)).Obligations.Select(item => item.DueDate).Order());
    }

    [Fact]
    public async Task CloseMonth_PreventsDuplicateConfirmedClose()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(
            repository,
            new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        Guid collaboratorId = Guid.NewGuid();
        var input = new MonthlySummaryInput(10_000, 0, 0, 5_000, 0, 0, 0, 0, 0, 0, 0);

        var first = await service.CloseMonthAsync(
            new YearMonth(2026, 7), input, Percentage.FromPercent(20m), [collaboratorId], cancellationToken);

        Assert.Single(first.Participants);
        Assert.Equal(1_000, first.Participants[0].Amount.MinorUnits);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CloseMonthAsync(
            new YearMonth(2026, 7), input, Percentage.FromPercent(20m), [collaboratorId], cancellationToken));
    }

    [Fact]
    public async Task ReopenWithoutPayments_InvalidatesParticipantsAndAllowsCleanNewClose()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        Guid first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        Guid second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var input = new MonthlySummaryInput(10_000, 0, 0, 5_000, 0, 0, 0, 0, 0, 0, 0);
        var original = await service.CloseMonthAsync(
            new YearMonth(2026, 7), input, Percentage.FromPercent(20m), [first, second], cancellationToken);

        await service.ReopenMonthAsync(original.Close.Id, cancellationToken);
        InvalidOperationException paymentError = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RegisterDistributionPaymentAsync(
                original.Participants[0].Id,
                new DateOnly(2026, 7, 31),
                Money.FromDecimal(1m),
                cancellationToken));
        var replacement = await service.CloseMonthAsync(
            new YearMonth(2026, 7), input, Percentage.FromPercent(20m), [first, second], cancellationToken);
        AdministrationData active = await service.LoadAsync(cancellationToken);

        Assert.Contains("ya no está disponible", paymentError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, replacement.Participants.Count);
        Assert.Equal(replacement.Close.FundMinorUnits, replacement.Participants.Sum(item => item.Amount.MinorUnits));
        Assert.Equal(2, active.MonthlyCloseParticipants.Count);
        Assert.Single(active.MonthlyCloses, item => item.IsConfirmed);
    }

    [Fact]
    public async Task ReopenWithDistributionPayment_IsBlocked()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        var input = new MonthlySummaryInput(10_000, 0, 0, 5_000, 0, 0, 0, 0, 0, 0, 0);
        var closed = await service.CloseMonthAsync(
            new YearMonth(2026, 7), input, Percentage.FromPercent(20m), [Guid.NewGuid()], cancellationToken);
        await service.RegisterDistributionPaymentAsync(
            closed.Participants[0].Id, new DateOnly(2026, 7, 31), Money.FromDecimal(10m), cancellationToken);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ReopenMonthAsync(closed.Close.Id, cancellationToken));

        Assert.Contains("pagos de distribución", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(closed.Close.IsConfirmed);
    }

    [Fact]
    public async Task ProductNames_AreUniqueIgnoringCase()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        await service.AddProductAsync(
            Product.Create("Agua", ProductCategory.ProductForSale, "unidad", UtcNow), cancellationToken);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddProductAsync(
                Product.Create(" agua ", ProductCategory.ProductForSale, "unidad", UtcNow), cancellationToken));

        Assert.Contains("Ya existe", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeleteParentWithRelations_IsRejectedWithClearMessage()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        Obligation obligation = Obligation.Create(
            "Impuesto", ObligationType.Tax, new DateOnly(2026, 7, 1),
            Money.FromDecimal(20m), RecurrenceFrequency.None, UtcNow);
        await service.AddAsync(obligation, cancellationToken);
        await service.AddAsync(ObligationPayment.Create(
            obligation.Id, new DateOnly(2026, 7, 2), Money.FromDecimal(5m), UtcNow), cancellationToken);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.DeleteAsync(obligation, cancellationToken));

        Assert.Contains("pagos registrados", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(obligation.IsDeleted);
    }

    [Fact]
    public async Task DeleteProtectedParentsAndComputedRecords_IsRejected()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));

        LocalUsePerson person = LocalUsePerson.Create("Ana", new DateOnly(2026, 7, 1), null, UtcNow);
        await service.AddLocalUsePersonAsync(person, new DateOnly(2026, 7, 1), cancellationToken);

        Product product = Product.Create("Agua", ProductCategory.ProductForSale, "unidad", UtcNow);
        await service.AddProductAsync(product, cancellationToken);
        await service.AddInventoryMovementAsync(InventoryMovement.Initial(
            product.Id, new DateOnly(2026, 7, 1), Quantity.Positive(2m), Money.FromDecimal(10m), UtcNow),
            cancellationToken);

        Collaborator collaborator = Collaborator.Create("Luis", new DateOnly(2026, 7, 1), null, UtcNow);
        await service.AddAsync(collaborator, cancellationToken);
        var closed = await service.CloseMonthAsync(
            new YearMonth(2026, 7),
            new MonthlySummaryInput(10_000, 0, 0, 5_000, 0, 0, 0, 0, 0, 0, 0),
            Percentage.FromPercent(20m),
            [collaborator.Id],
            cancellationToken);

        InvalidOperationException personError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(person, cancellationToken));
        InvalidOperationException productError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(product, cancellationToken));
        InvalidOperationException collaboratorError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(collaborator, cancellationToken));
        InvalidOperationException closeError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(closed.Close, cancellationToken));
        InvalidOperationException participantError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(closed.Participants[0], cancellationToken));

        Assert.Contains("cuotas", personError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("movimientos", productError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cierres", collaboratorError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("reapertura", closeError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no se eliminan", participantError.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InventoryCorrection_RejectsAChronologyThatWouldMakeStockNegative()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        Product product = Product.Create("Agua", ProductCategory.ProductForSale, "unidad", UtcNow);
        InventoryMovement initial = InventoryMovement.Initial(
            product.Id, new DateOnly(2026, 7, 1), Quantity.Positive(10m), Money.FromDecimal(50m), UtcNow);
        InventoryMovement sale = InventoryMovement.Sale(
            product.Id, new DateOnly(2026, 7, 2), Quantity.Positive(5m), Money.FromDecimal(5m),
            Money.FromDecimal(5m), 10m, UtcNow.AddMinutes(1));
        await service.AddProductAsync(product, cancellationToken);
        await service.AddInventoryMovementAsync(initial, cancellationToken);
        await service.AddInventoryMovementAsync(sale, cancellationToken);
        initial.Correct(
            new DateOnly(2026, 7, 3), 10m, null, Money.FromDecimal(50m), UtcNow.AddMinutes(2));

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdateInventoryMovementAsync(initial, cancellationToken));

        Assert.Contains("negativo", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmedCloseSnapshot_SurvivesPercentageChangeAndReopenRestoresDynamicCalculation()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        var month = new YearMonth(2026, 7);
        var input = new MonthlySummaryInput(10_000, 0, 0, 5_000, 0, 0, 0, 0, 0, 0, 0);
        var closed = await service.CloseMonthAsync(
            month, input, Percentage.FromPercent(20m), [Guid.NewGuid()], cancellationToken);
        AdministrationData data = await service.LoadAsync(cancellationToken);

        MonthlySummaryResult snapshot = AdministrationReports.MonthlySummary(
            data, Money.FromDecimal(999m), Percentage.FromPercent(50m), month);
        await service.ReopenMonthAsync(closed.Close.Id, cancellationToken);
        MonthlySummaryResult dynamic = AdministrationReports.MonthlySummary(
            await service.LoadAsync(cancellationToken), Money.FromDecimal(0m), Percentage.FromPercent(50m), month);

        Assert.Equal(1_000, snapshot.CollaboratorFundMinorUnits);
        Assert.Equal(0, dynamic.CollaboratorFundMinorUnits);
    }

    [Fact]
    public async Task HomeCapacityAndAnnualBreakdown_FollowApprovedRules()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var service = CreateService(repository, new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow)));
        LocalUsePerson person = LocalUsePerson.Create("Ana", new DateOnly(2026, 7, 1), null, UtcNow);
        await service.AddLocalUsePersonAsync(person, new DateOnly(2026, 7, 18), cancellationToken);
        await service.AddAsync(Obligation.Create(
            "Servicio vencido", ObligationType.Service, new DateOnly(2026, 6, 1), Money.FromDecimal(10m), RecurrenceFrequency.None, UtcNow), cancellationToken);
        await service.AddAsync(Obligation.Create(
            "Impuesto del mes", ObligationType.Tax, new DateOnly(2026, 7, 25), Money.FromDecimal(20m), RecurrenceFrequency.None, UtcNow), cancellationToken);
        await service.AddAsync(Obligation.Create(
            "Otra", ObligationType.OtherRecurring, new DateOnly(2026, 7, 20), Money.FromDecimal(30m), RecurrenceFrequency.None, UtcNow), cancellationToken);
        await service.AddAsync(Obligation.Create(
            "Servicio futuro", ObligationType.Service, new DateOnly(2026, 8, 1), Money.FromDecimal(40m), RecurrenceFrequency.None, UtcNow), cancellationToken);
        AdministrationData data = await service.LoadAsync(cancellationToken);

        HomeDashboard home = HomeDashboardCalculator.Calculate(
            data, Money.FromDecimal(0m), Percentage.FromPercent(20m), new DateOnly(2026, 7, 18));
        ChairCapacity capacity = HomeDashboardCalculator.Capacity(data, 0, new DateOnly(2026, 7, 18));
        AnnualAdministrationReport annual = AdministrationReports.Annual(
            data, Money.FromDecimal(0m), Percentage.FromPercent(20m), 2026);

        Assert.Equal(["Servicio vencido", "Impuesto del mes"], home.Obligations.Select(item => item.Name));
        Assert.Equal(1, capacity.Overcapacity);
        Assert.Equal(5_000, annual.Expenses.ServicesMinorUnits);
        Assert.Equal(2_000, annual.Expenses.TaxesMinorUnits);
        Assert.Equal(3_000, annual.Expenses.OtherObligationsMinorUnits);
        Assert.Contains(annual.Indicator, new[] { "Positivo", "Negativo" });
    }

    [Fact]
    public async Task SaveSettings_RecordsNewRateOnlyWhenFeeChanges()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new FakeAdministrationRepository();
        var settingsRepository = new FakeSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var useCase = new SaveSettingsUseCase(
            settingsRepository,
            repository,
            new FixedTimeProvider(new DateTimeOffset(UtcNow.AddDays(1))));

        await useCase.ExecuteAsync(new SaveSettingsRequest(15m, 20m, 0m, 0, "USD"), cancellationToken);
        await useCase.ExecuteAsync(new SaveSettingsRequest(15m, 25m, 0m, 0, "USD"), cancellationToken);

        Assert.Single(repository.Entities.OfType<WeeklyRate>());
        Assert.Equal(1_500, repository.Entities.OfType<WeeklyRate>().Single().Amount.MinorUnits);
        Assert.Equal(2_500, settingsRepository.Settings.CollaboratorProfit.BasisPoints);
    }

    private static AdministrationService CreateService(
        FakeAdministrationRepository repository,
        FakeSettingsRepository settingsRepository) => new(
            repository,
            settingsRepository,
            new FixedTimeProvider(new DateTimeOffset(UtcNow)));

    private sealed class FakeSettingsRepository(GeneralSettings settings) : ISettingsRepository
    {
        public GeneralSettings Settings { get; } = settings;

        public Task<GeneralSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Settings);

        public Task SaveAsync(GeneralSettings settings, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeAdministrationRepository : IAdministrationRepository
    {
        public List<AuditableEntity> Entities { get; } = [];

        public bool LastSaveWasSingleTransaction { get; private set; }

        public Task<AdministrationData> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdministrationData(
                Entities.OfType<LocalUsePerson>().Where(Active).ToArray(),
                Entities.OfType<WeeklyRate>().Where(Active).ToArray(),
                Entities.OfType<WeeklyCharge>().Where(Active).ToArray(),
                Entities.OfType<LocalUsePayment>().Where(Active).ToArray(),
                Entities.OfType<Product>().Where(Active).ToArray(),
                Entities.OfType<InventoryMovement>().Where(Active).ToArray(),
                Entities.OfType<MonthlyRestockPlan>().Where(Active).ToArray(),
                Entities.OfType<FinancialEntry>().Where(Active).ToArray(),
                Entities.OfType<Obligation>().Where(Active).ToArray(),
                Entities.OfType<ObligationPayment>().Where(Active).ToArray(),
                Entities.OfType<MaintenanceRecord>().Where(Active).ToArray(),
                Entities.OfType<Collaborator>().Where(Active).ToArray(),
                Entities.OfType<MonthlyClose>().Where(Active).ToArray(),
                Entities.OfType<MonthlyCloseParticipant>().Where(Active).ToArray(),
                Entities.OfType<DistributionPayment>().Where(Active).ToArray()));

        public Task SaveAsync(
            IReadOnlyCollection<AuditableEntity> additions,
            IReadOnlyCollection<AuditableEntity> updates,
            CancellationToken cancellationToken = default)
        {
            Entities.AddRange(additions);
            LastSaveWasSingleTransaction = true;
            return Task.CompletedTask;
        }

        public Task SaveSettingsAndRateAsync(
            GeneralSettings settings,
            WeeklyRate? newRate,
            CancellationToken cancellationToken = default)
        {
            if (newRate is not null)
            {
                Entities.Add(newRate);
            }

            LastSaveWasSingleTransaction = true;
            return Task.CompletedTask;
        }

        private static bool Active(AuditableEntity entity) => !entity.IsDeleted;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
