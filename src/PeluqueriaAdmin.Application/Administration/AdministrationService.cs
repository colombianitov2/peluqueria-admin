using PeluqueriaAdmin.Application.Settings;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Reports;
using PeluqueriaAdmin.Domain.Settings;

namespace PeluqueriaAdmin.Application.Administration;

public sealed class AdministrationService(
    IAdministrationRepository repository,
    ISettingsRepository settingsRepository,
    TimeProvider timeProvider)
{
    public Task<AdministrationData> LoadAsync(CancellationToken cancellationToken = default) =>
        repository.LoadAsync(cancellationToken);

    public async Task<AdministrationData> GenerateScheduledRecordsAsync(
        DateOnly throughDate,
        CancellationToken cancellationToken = default)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var additions = new List<AuditableEntity>();
        IReadOnlyCollection<WeeklyRate> rates = data.WeeklyRates;

        if (rates.Count == 0)
        {
            GeneralSettings settings = await settingsRepository.GetAsync(cancellationToken);
            WeeklyRate initialRate = WeeklyRate.Create(
                DateOnly.FromDateTime(settings.CreatedUtc),
                settings.WeeklyUsageFee,
                utcNow);
            additions.Add(initialRate);
            rates = [initialRate];
        }

        foreach (LocalUsePerson person in data.LocalUsePeople)
        {
            additions.AddRange(WeeklyChargeCalculator.Generate(
                person,
                data.WeeklyCharges,
                rates,
                throughDate,
                utcNow));
        }

        foreach (IGrouping<Guid, Obligation> series in data.Obligations.GroupBy(item => item.SeriesId))
        {
            Obligation template = series.OrderBy(item => item.DueDate).First();
            additions.AddRange(ObligationRecurrenceGenerator.Generate(
                template,
                series,
                throughDate,
                utcNow));
        }

        if (additions.Count > 0)
        {
            await repository.SaveAsync(additions, [], cancellationToken);
        }

        return await repository.LoadAsync(cancellationToken);
    }

    public Task AddAsync(AuditableEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return repository.SaveAsync([entity], [], cancellationToken);
    }

    public Task UpdateAsync(AuditableEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return repository.SaveAsync([], [entity], cancellationToken);
    }

    public Task DeleteAsync(AuditableEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        entity.MarkDeleted(timeProvider.GetUtcNow().UtcDateTime);
        return repository.SaveAsync([], [entity], cancellationToken);
    }

    public async Task<LocalUsePayment> RegisterLocalUsePaymentAsync(
        Guid personId,
        DateOnly date,
        Money amount,
        CancellationToken cancellationToken = default)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        Money debt = WeeklyChargeCalculator.CalculateDebt(
            data.WeeklyCharges.Where(item => item.PersonId == personId),
            data.LocalUsePayments.Where(item => item.PersonId == personId));
        LocalUsePayment payment = LocalUsePayment.Create(
            personId,
            date,
            amount,
            debt,
            timeProvider.GetUtcNow().UtcDateTime);
        await repository.SaveAsync([payment], [], cancellationToken);
        return payment;
    }

    public async Task AddInventoryMovementAsync(
        InventoryMovement movement,
        CancellationToken cancellationToken = default)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        InventoryMovement[] productMovements = data.InventoryMovements
            .Where(item => item.ProductId == movement.ProductId)
            .Append(movement)
            .ToArray();
        InventoryCalculator.EnsureNonNegative(productMovements);
        await repository.SaveAsync([movement], [], cancellationToken);
    }

    public async Task<(MonthlyClose Close, IReadOnlyList<MonthlyCloseParticipant> Participants)> CloseMonthAsync(
        YearMonth month,
        MonthlySummaryInput input,
        Percentage percentage,
        IReadOnlyCollection<Guid> participantIds,
        CancellationToken cancellationToken = default)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        if (data.MonthlyCloses.Any(close => close.Month == month && close.IsConfirmed))
        {
            throw new InvalidOperationException("El mes ya tiene un cierre confirmado.");
        }

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        MonthlySummaryResult summary = MonthlySummaryCalculator.Calculate(input, percentage);
        MonthlyClose close = MonthlyClose.Create(month, percentage, summary, utcNow);
        IReadOnlyList<MonthlyCloseParticipant> participants =
            CollaboratorDistributionCalculator.Distribute(close, participantIds, utcNow);
        await repository.SaveAsync(
            new AuditableEntity[] { close }.Concat(participants).ToArray(),
            [],
            cancellationToken);
        return (close, participants);
    }
}
