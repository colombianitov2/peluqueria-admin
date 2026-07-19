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

    public Task AddAsync(
        AuditableEntity entity,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return SaveAsync([entity], [], completedDraftKey, cancellationToken);
    }

    public async Task AddLocalUsePersonAsync(
        LocalUsePerson person,
        DateOnly throughDate,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        ArgumentNullException.ThrowIfNull(person);
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        (IReadOnlyCollection<WeeklyRate> rates, WeeklyRate? newRate) =
            await EnsureRatesAsync(data, cancellationToken);
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        IReadOnlyList<WeeklyCharge> charges = WeeklyChargeCalculator.Generate(
            person, [], rates, throughDate, utcNow);
        await SaveAsync(
            new AuditableEntity[] { person }
                .Concat(newRate is null ? [] : [newRate])
                .Concat(charges)
                .ToArray(),
            [],
            completedDraftKey,
            cancellationToken);
    }

    public async Task UpdateLocalUsePersonAsync(
        Guid personId,
        string name,
        DateOnly entryDate,
        DateOnly? exitDate,
        DateOnly throughDate,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        LocalUsePerson person = data.LocalUsePeople.SingleOrDefault(item => item.Id == personId)
            ?? throw new InvalidOperationException("La persona seleccionada ya no está disponible.");
        HashSet<DateOnly> expected = WeeklyChargeCalculator.ExpectedPeriodStarts(entryDate, exitDate, throughDate).ToHashSet();
        WeeklyCharge[] existing = data.WeeklyCharges.Where(item => item.PersonId == personId).ToArray();
        WeeklyCharge[] invalid = existing.Where(item => !expected.Contains(item.PeriodStart)).ToArray();
        if (invalid.Length > 0 && data.LocalUsePayments.Any(item => item.PersonId == personId))
        {
            throw new InvalidOperationException(
                "No se pueden cambiar ingreso o retiro porque invalidarían cuotas de una persona que ya tiene pagos.");
        }

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        person.Update(name, entryDate, exitDate, utcNow);
        foreach (WeeklyCharge charge in invalid)
        {
            charge.MarkDeleted(utcNow);
        }

        (IReadOnlyCollection<WeeklyRate> rates, WeeklyRate? newRate) =
            await EnsureRatesAsync(data, cancellationToken);
        IReadOnlyList<WeeklyCharge> additions = WeeklyChargeCalculator.Generate(
            person, existing, rates, throughDate, utcNow);
        await SaveAsync(
            (newRate is null ? Array.Empty<AuditableEntity>() : [newRate])
                .Concat(additions)
                .ToArray(),
            new AuditableEntity[] { person }.Concat(invalid).ToArray(),
            completedDraftKey,
            cancellationToken);
    }

    public async Task AddObligationAsync(
        Obligation obligation,
        DateOnly throughDate,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        ArgumentNullException.ThrowIfNull(obligation);
        IReadOnlyList<Obligation> occurrences = ObligationRecurrenceGenerator.Generate(
            obligation, [obligation], throughDate, timeProvider.GetUtcNow().UtcDateTime);
        await SaveAsync(
            new AuditableEntity[] { obligation }.Concat(occurrences).ToArray(),
            [],
            completedDraftKey,
            cancellationToken);
    }

    public async Task AddProductAsync(
        Product product,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        ArgumentNullException.ThrowIfNull(product);
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        EnsureUniqueProductName(data, product.Name, null);
        await SaveAsync([product], [], completedDraftKey, cancellationToken);
    }

    public async Task UpdateProductAsync(
        Guid productId,
        string name,
        ProductCategory category,
        string unitOfMeasure,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        Product product = data.Products.SingleOrDefault(item => item.Id == productId)
            ?? throw new InvalidOperationException("El producto seleccionado ya no está disponible.");
        EnsureUniqueProductName(data, name, productId);
        product.Update(name, category, unitOfMeasure, timeProvider.GetUtcNow().UtcDateTime);
        await SaveAsync([], [product], completedDraftKey, cancellationToken);
    }

    public Task UpdateAsync(
        AuditableEntity entity,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return SaveAsync([], [entity], completedDraftKey, cancellationToken);
    }

    public async Task DeleteAsync(AuditableEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        string? blockedReason = entity switch
        {
            LocalUsePerson person when data.WeeklyCharges.Any(item => item.PersonId == person.Id)
                || data.LocalUsePayments.Any(item => item.PersonId == person.Id) =>
                "No se puede eliminar la persona porque tiene cuotas o pagos históricos.",
            Product product when data.InventoryMovements.Any(item => item.ProductId == product.Id)
                || data.RestockPlans.Any(item => item.ProductId == product.Id) =>
                "No se puede eliminar el producto porque tiene movimientos o planes mensuales.",
            Obligation obligation when data.ObligationPayments.Any(item => item.ObligationId == obligation.Id) =>
                "No se puede eliminar la obligación porque tiene pagos registrados.",
            Collaborator collaborator when data.MonthlyCloseParticipants.Any(item => item.CollaboratorId == collaborator.Id) =>
                "No se puede eliminar el colaborador porque participa en cierres históricos.",
            MonthlyClose => "Los cierres mensuales no se eliminan; usa la reapertura segura.",
            MonthlyCloseParticipant => "Las asignaciones calculadas no se eliminan manualmente.",
            _ => null,
        };
        if (blockedReason is not null)
        {
            throw new InvalidOperationException(blockedReason);
        }

        entity.MarkDeleted(timeProvider.GetUtcNow().UtcDateTime);
        await repository.SaveAsync([], [entity], cancellationToken);
    }

    public async Task<LocalUsePayment> RegisterLocalUsePaymentAsync(
        Guid personId,
        DateOnly date,
        Money amount,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
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
        await SaveAsync([payment], [], completedDraftKey, cancellationToken);
        return payment;
    }

    public async Task AddInventoryMovementAsync(
        InventoryMovement movement,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        InventoryMovement[] productMovements = data.InventoryMovements
            .Where(item => item.ProductId == movement.ProductId)
            .Append(movement)
            .ToArray();
        InventoryCalculator.EnsureNonNegative(productMovements);
        await SaveAsync([movement], [], completedDraftKey, cancellationToken);
    }

    public async Task UpdateInventoryMovementAsync(
        InventoryMovement movement,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        InventoryMovement[] productMovements = data.InventoryMovements
            .Where(item => item.ProductId == movement.ProductId && item.Id != movement.Id)
            .Append(movement)
            .ToArray();
        InventoryCalculator.EnsureNonNegative(productMovements);
        await SaveAsync([], [movement], completedDraftKey, cancellationToken);
    }

    public async Task DeleteInventoryMovementAsync(
        InventoryMovement movement,
        CancellationToken cancellationToken = default)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        movement.MarkDeleted(timeProvider.GetUtcNow().UtcDateTime);
        InventoryCalculator.EnsureNonNegative(data.InventoryMovements
            .Where(item => item.ProductId == movement.ProductId && item.Id != movement.Id));
        await repository.SaveAsync([], [movement], cancellationToken);
    }

    public async Task<(MonthlyClose Close, IReadOnlyList<MonthlyCloseParticipant> Participants)> CloseMonthAsync(
        YearMonth month,
        MonthlySummaryInput input,
        Percentage percentage,
        IReadOnlyCollection<Guid> participantIds,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
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
        await SaveAsync(
            new AuditableEntity[] { close }.Concat(participants).ToArray(),
            [],
            completedDraftKey,
            cancellationToken);
        return (close, participants);
    }

    public async Task ReopenMonthAsync(Guid closeId, CancellationToken cancellationToken = default)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        MonthlyClose close = data.MonthlyCloses.SingleOrDefault(item => item.Id == closeId)
            ?? throw new InvalidOperationException("El cierre seleccionado ya no está disponible.");
        MonthlyCloseParticipant[] participants = data.MonthlyCloseParticipants
            .Where(item => item.CloseId == close.Id)
            .ToArray();
        Guid[] participantIds = participants.Select(item => item.Id).ToArray();
        if (data.DistributionPayments.Any(item => participantIds.Contains(item.ParticipantId)))
        {
            throw new InvalidOperationException(
                "No se puede reabrir el cierre porque ya tiene pagos de distribución registrados.");
        }

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        close.Reopen(utcNow);
        foreach (MonthlyCloseParticipant participant in participants)
        {
            participant.MarkDeleted(utcNow);
        }

        await repository.SaveAsync([], new AuditableEntity[] { close }.Concat(participants).ToArray(), cancellationToken);
    }

    public async Task<DistributionPayment> RegisterDistributionPaymentAsync(
        Guid participantId,
        DateOnly date,
        Money amount,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        MonthlyCloseParticipant participant = data.MonthlyCloseParticipants
            .SingleOrDefault(item => item.Id == participantId)
            ?? throw new InvalidOperationException("La asignación seleccionada ya no está disponible.");
        MonthlyClose close = data.MonthlyCloses.SingleOrDefault(item => item.Id == participant.CloseId)
            ?? throw new InvalidOperationException("El cierre de la asignación ya no está disponible.");
        if (!close.IsConfirmed)
        {
            throw new InvalidOperationException("Solo se pueden pagar asignaciones de cierres confirmados.");
        }

        long paid = data.DistributionPayments
            .Where(item => item.ParticipantId == participant.Id)
            .Sum(item => item.Amount.MinorUnits);
        Money pending = Money.FromMinorUnits(participant.Amount.MinorUnits - paid);
        DistributionPayment payment = DistributionPayment.Create(
            participant.Id, date, amount, pending, timeProvider.GetUtcNow().UtcDateTime);
        await SaveAsync([payment], [], completedDraftKey, cancellationToken);
        return payment;
    }

    private Task SaveAsync(
        IReadOnlyCollection<AuditableEntity> additions,
        IReadOnlyCollection<AuditableEntity> updates,
        string? completedDraftKey,
        CancellationToken cancellationToken) => string.IsNullOrWhiteSpace(completedDraftKey)
        ? repository.SaveAsync(additions, updates, cancellationToken)
        : repository.SaveCompletingDraftAsync(additions, updates, completedDraftKey, cancellationToken);

    private async Task<(IReadOnlyCollection<WeeklyRate> Rates, WeeklyRate? NewRate)> EnsureRatesAsync(
        AdministrationData data,
        CancellationToken cancellationToken)
    {
        if (data.WeeklyRates.Count > 0)
        {
            return (data.WeeklyRates, null);
        }

        GeneralSettings settings = await settingsRepository.GetAsync(cancellationToken);
        WeeklyRate rate = WeeklyRate.Create(
            DateOnly.FromDateTime(settings.CreatedUtc),
            settings.WeeklyUsageFee,
            timeProvider.GetUtcNow().UtcDateTime);
        return ([rate], rate);
    }

    private static void EnsureUniqueProductName(AdministrationData data, string name, Guid? exceptId)
    {
        string normalized = name.Trim();
        if (data.Products.Any(item => item.Id != exceptId
            && string.Equals(item.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Ya existe un producto con ese nombre.");
        }
    }
}
