using PeluqueriaAdmin.App.ViewModels;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.App.Tests;

public sealed class ObligationCreditWeeklyTests
{
    private static readonly DateTime UtcNow = new(2026, 7, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ViewModel_OffersAndRoundTripsCreditWithWeeklyRecurrence()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var repository = new RecordingRepository();
        var settings = new FixedSettingsRepository(GeneralSettings.CreateDefault(UtcNow));
        var timeProvider = new FixedTimeProvider(new DateTimeOffset(UtcNow));
        var service = new AdministrationService(repository, settings, timeProvider);
        var viewModel = new ObligationsViewModel(service, timeProvider);

        Assert.Contains("Crédito", viewModel.TypeOptions);
        Assert.Contains("Semanal", viewModel.RecurrenceOptions);

        viewModel.NameText = "Crédito semanal";
        viewModel.SelectedType = "Crédito";
        viewModel.SelectedRecurrence = "Semanal";
        viewModel.InitialDueDate = new DateTime(2026, 7, 23);
        viewModel.ExpectedAmountText = "25";

        await viewModel.AddObligationCommand.ExecuteAsync(null);

        Obligation[] saved = (await service.LoadAsync(cancellationToken)).Obligations
            .OrderBy(item => item.DueDate)
            .ToArray();
        Assert.Equal(
            [new DateOnly(2026, 7, 23), new DateOnly(2026, 7, 30)],
            saved.Select(item => item.DueDate));
        Assert.All(saved, item =>
        {
            Assert.Equal(ObligationType.Credit, item.Type);
            Assert.Equal(RecurrenceFrequency.Weekly, item.Recurrence);
        });
        ObligationCatalogRow row = Assert.Single(viewModel.Obligations);
        Assert.Equal("Crédito", row.Type);
        Assert.Equal("Semanal", row.Recurrence);
        Assert.False(viewModel.IsError, viewModel.StatusMessage);

        await viewModel.RefreshCommand.ExecuteAsync(null);
        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, (await service.LoadAsync(cancellationToken)).Obligations.Count);
        Assert.Single(viewModel.Obligations);
    }

    private sealed class RecordingRepository : IAdministrationRepository
    {
        private readonly List<AuditableEntity> entities = [];

        public Task<AdministrationData> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AdministrationData(
                Active<LocalUsePerson>(), Active<WeeklyRate>(), Active<WeeklyCharge>(), Active<LocalUsePayment>(),
                [], [], [], [], Active<Obligation>(), Active<ObligationPayment>(), [], [], [], [], [],
                [], [], [], [], [], [], [], [], [], [], [], [], []));

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

        private T[] Active<T>() where T : AuditableEntity => entities
            .OfType<T>()
            .Where(item => !item.IsDeleted)
            .ToArray();
    }

    private sealed class FixedSettingsRepository(GeneralSettings settings) : ISettingsRepository
    {
        public Task<GeneralSettings> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(settings);

        public Task SaveAsync(
            GeneralSettings value,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
