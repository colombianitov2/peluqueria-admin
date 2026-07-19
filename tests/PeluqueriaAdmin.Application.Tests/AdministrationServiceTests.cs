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
