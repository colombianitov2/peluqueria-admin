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
        DateOnly localToday = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        DateOnly weeklyThroughDate = throughDate < localToday ? throughDate : localToday;
        var additions = new List<AuditableEntity>();
        var updates = new List<AuditableEntity>();
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
                weeklyThroughDate,
                utcNow));
        }

        foreach (Chair chair in data.Chairs)
        {
            if (chair.AssignedPersonId is not Guid assignedPersonId) continue;
            LocalUsePerson? assigned = data.LocalUsePeople.SingleOrDefault(
                person => person.Id == assignedPersonId);
            if (assigned is null || !assigned.IsCurrentOn(localToday))
            {
                chair.Unassign(utcNow);
                updates.Add(chair);
            }
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

        if (additions.Count > 0 || updates.Count > 0)
        {
            await repository.SaveAsync(additions, updates, cancellationToken);
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

    public async Task AddLocalUsePersonWithChairAsync(
        LocalUsePerson person,
        Guid chairId,
        DateOnly throughDate,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        ArgumentNullException.ThrowIfNull(person);
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        if (data.Chairs.All(item => item.AssignedPersonId.HasValue))
        {
            throw new InvalidOperationException(
                "No hay sillas disponibles. Debes crear un espacio para una silla adicional.");
        }

        Chair chair = data.Chairs.SingleOrDefault(item => item.Id == chairId)
            ?? throw new InvalidOperationException("La silla seleccionada ya no está disponible.");
        if (chair.AssignedPersonId.HasValue)
        {
            throw new InvalidOperationException("La silla seleccionada ya está ocupada.");
        }

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        chair.Assign(person.Id, utcNow);
        (IReadOnlyCollection<WeeklyRate> rates, WeeklyRate? newRate) =
            await EnsureRatesAsync(data, cancellationToken);
        IReadOnlyList<WeeklyCharge> charges = WeeklyChargeCalculator.Generate(
            person, [], rates, throughDate, utcNow);
        ActivityRecord assignment = ActivityRecord.Create(
            person.EntryDate,
            "Uso del local",
            "Asignación de silla",
            $"{person.Name} → {chair.Name}",
            person.Id,
            chair.Description,
            utcNow);
        await SaveAsync(
            new AuditableEntity[] { person, assignment }
                .Concat(newRate is null ? [] : [newRate])
                .Concat(charges)
                .ToArray(),
            [chair],
            completedDraftKey,
            cancellationToken);
    }

    public async Task AddChairAsync(
        Chair chair,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        ArgumentNullException.ThrowIfNull(chair);
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        EnsureUniqueChairName(data, chair.Name, null);
        await SaveAsync([chair], [], completedDraftKey, cancellationToken);
    }

    public async Task UpdateChairAsync(
        Guid chairId,
        string name,
        DateOnly creationDate,
        string? description,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        Chair chair = data.Chairs.SingleOrDefault(item => item.Id == chairId)
            ?? throw new InvalidOperationException("La silla seleccionada ya no está disponible.");
        EnsureUniqueChairName(data, name, chairId);
        chair.Update(name, creationDate, description, timeProvider.GetUtcNow().UtcDateTime);
        await SaveAsync([], [chair], completedDraftKey, cancellationToken);
    }

    public async Task AssignChairAsync(
        Guid personId,
        Guid? chairId,
        DateOnly today,
        CancellationToken cancellationToken = default)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        LocalUsePerson person = data.LocalUsePeople.SingleOrDefault(item => item.Id == personId)
            ?? throw new InvalidOperationException("El trabajador seleccionado ya no está disponible.");
        if (!person.IsCurrentOn(today))
        {
            throw new InvalidOperationException("Solo un trabajador vigente puede ocupar una silla.");
        }

        Chair? target = chairId.HasValue
            ? data.Chairs.SingleOrDefault(item => item.Id == chairId.Value)
                ?? throw new InvalidOperationException("La silla seleccionada ya no está disponible.")
            : null;
        if (target?.AssignedPersonId is Guid assigned && assigned != personId)
        {
            throw new InvalidOperationException("La silla seleccionada ya está ocupada.");
        }

        Chair? current = data.Chairs.SingleOrDefault(item => item.AssignedPersonId == personId);
        if (target is null && current is null)
        {
            return;
        }

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var updates = new List<AuditableEntity>();
        if (current is not null && current.Id != target?.Id)
        {
            current.Unassign(utcNow);
            updates.Add(current);
        }

        if (target is not null && target.AssignedPersonId != personId)
        {
            target.Assign(personId, utcNow);
            updates.Add(target);
        }

        string action = target is null ? "Retiro de silla" : current is null
            ? "Asignación de silla"
            : "Cambio de silla";
        string summary = target is null
            ? $"{person.Name} dejó {current!.Name}"
            : $"{person.Name} → {target.Name}";
        ActivityRecord activity = ActivityRecord.Create(
            today, "Uso del local", action, summary, person.Id, null, utcNow);
        await repository.SaveAsync([activity], updates, cancellationToken);
    }

    public async Task UpdateLocalUsePersonAsync(
        Guid personId,
        string name,
        DateOnly entryDate,
        DateOnly? exitDate,
        DateOnly throughDate,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null,
        string? description = null)
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
        person.Update(name, entryDate, exitDate, utcNow, description);
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

    public async Task RetireLocalUsePersonAsync(
        Guid personId,
        DateOnly exitDate,
        CancellationToken cancellationToken = default)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        LocalUsePerson person = data.LocalUsePeople.SingleOrDefault(item => item.Id == personId)
            ?? throw new InvalidOperationException("El trabajador seleccionado ya no está disponible.");
        if (exitDate < person.EntryDate)
        {
            throw new ArgumentException("La fecha de retiro no puede ser anterior a la fecha de ingreso.", nameof(exitDate));
        }

        HashSet<DateOnly> expected = WeeklyChargeCalculator
            .ExpectedPeriodStarts(person.EntryDate, exitDate, exitDate)
            .ToHashSet();
        WeeklyCharge[] existing = data.WeeklyCharges.Where(item => item.PersonId == personId).ToArray();
        WeeklyCharge[] invalid = existing.Where(item => !expected.Contains(item.PeriodStart)).ToArray();
        if (invalid.Length > 0 && data.LocalUsePayments.Any(item => item.PersonId == personId))
        {
            throw new InvalidOperationException(
                "No se puede retirar al trabajador en esa fecha porque invalidaría cuotas que ya tienen pagos.");
        }

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        person.Update(person.Name, person.EntryDate, exitDate, utcNow, person.Description);
        foreach (WeeklyCharge charge in invalid)
        {
            charge.MarkDeleted(utcNow);
        }

        Chair? chair = data.Chairs.SingleOrDefault(item => item.AssignedPersonId == personId);
        if (chair is not null)
        {
            chair.Unassign(utcNow);
        }

        (IReadOnlyCollection<WeeklyRate> rates, WeeklyRate? newRate) =
            await EnsureRatesAsync(data, cancellationToken);
        IReadOnlyList<WeeklyCharge> additions = WeeklyChargeCalculator.Generate(
            person, existing, rates, exitDate, utcNow);
        ActivityRecord activity = ActivityRecord.Create(
            exitDate,
            "Uso del local",
            "Retiro del local",
            person.Name,
            person.Id,
            person.Description,
            utcNow);
        AuditableEntity[] added = (newRate is null ? Array.Empty<AuditableEntity>() : [newRate])
            .Concat(additions)
            .Append(activity)
            .ToArray();
        AuditableEntity[] updated = new AuditableEntity[] { person }
            .Concat(invalid)
            .Concat(chair is null ? [] : [chair])
            .ToArray();
        await repository.SaveAsync(added, updated, cancellationToken);
    }

    public async Task DeleteLocalUsePersonAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        LocalUsePerson person = data.LocalUsePeople.SingleOrDefault(item => item.Id == personId)
            ?? throw new InvalidOperationException("El trabajador seleccionado ya no está disponible.");
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        Chair? chair = data.Chairs.SingleOrDefault(item => item.AssignedPersonId == personId);
        if (chair is not null)
        {
            chair.Unassign(utcNow);
        }

        person.MarkDeleted(utcNow);
        ActivityRecord activity = ActivityRecord.Create(
            DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
            "Uso del local",
            "Eliminación lógica de trabajador",
            person.Name,
            person.Id,
            "El historial de cuotas y pagos se conserva; la silla quedó disponible.",
            utcNow);
        await repository.SaveAsync(
            [activity],
            new AuditableEntity[] { person }.Concat(chair is null ? [] : [chair]).ToArray(),
            cancellationToken);
    }

    public async Task DeleteCollaboratorAsync(
        Guid collaboratorId,
        CancellationToken cancellationToken = default)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        Collaborator collaborator = data.Collaborators.SingleOrDefault(item => item.Id == collaboratorId)
            ?? throw new InvalidOperationException("El colaborador seleccionado ya no está disponible.");
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        collaborator.MarkDeleted(utcNow);
        ActivityRecord activity = ActivityRecord.Create(
            DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
            "Colaboradores",
            "Eliminación lógica de colaborador",
            collaborator.Name,
            collaborator.Id,
            "Los aportes, cierres, distribuciones y pagos históricos se conservan.",
            utcNow);
        await repository.SaveAsync([activity], [collaborator], cancellationToken);
    }

    public async Task DeleteChairAsync(
        Guid chairId,
        CancellationToken cancellationToken = default)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        Chair chair = data.Chairs.SingleOrDefault(item => item.Id == chairId)
            ?? throw new InvalidOperationException("La silla seleccionada ya no está disponible.");
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        Guid? formerPersonId = chair.AssignedPersonId;
        if (formerPersonId.HasValue)
        {
            chair.Unassign(utcNow);
        }

        chair.MarkDeleted(utcNow);
        ActivityRecord activity = ActivityRecord.Create(
            DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
            "Uso del local",
            "Eliminación lógica de silla",
            chair.Name,
            chair.Id,
            formerPersonId.HasValue
                ? "La silla ocupada fue desasignada antes de eliminarse; el trabajador permanece sin silla."
                : "La silla vacía fue eliminada lógicamente.",
            utcNow);
        await repository.SaveAsync([activity], [chair], cancellationToken);
    }

    public async Task AddCollaboratorContributionAsync(
        CollaboratorContribution contribution,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        if (data.Collaborators.All(item => item.Id != contribution.CollaboratorId))
        {
            throw new InvalidOperationException("El colaborador seleccionado ya no está disponible.");
        }

        await SaveAsync([contribution], [], completedDraftKey, cancellationToken);
    }

    public async Task UpdateCollaboratorContributionAsync(
        Guid contributionId,
        DateOnly date,
        Money amount,
        string? description,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        CollaboratorContribution contribution = data.CollaboratorContributions
            .SingleOrDefault(item => item.Id == contributionId)
            ?? throw new InvalidOperationException("El aporte seleccionado ya no está disponible.");
        contribution.Update(date, amount, description, timeProvider.GetUtcNow().UtcDateTime);
        await SaveAsync([], [contribution], completedDraftKey, cancellationToken);
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

    public Task ScheduleMaintenanceAsync(
        MaintenanceRecord maintenance,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        ArgumentNullException.ThrowIfNull(maintenance);
        return SaveAsync([maintenance], [], completedDraftKey, cancellationToken);
    }

    public async Task CompleteMaintenanceAsync(
        Guid maintenanceId,
        DateOnly completedDate,
        Money actualCost,
        string? description = null,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        MaintenanceRecord maintenance = data.MaintenanceRecords.SingleOrDefault(item => item.Id == maintenanceId)
            ?? throw new InvalidOperationException("El mantenimiento seleccionado ya no está disponible.");
        if (maintenance.CompletedDate.HasValue)
        {
            return;
        }
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        maintenance.Complete(completedDate, actualCost, utcNow, description);
        MaintenanceRecord[] additions = maintenance.IsRecurring
            && !data.MaintenanceRecords.Any(item => item.SeriesId == maintenance.SeriesId
                && item.OccurrenceNumber == maintenance.OccurrenceNumber + 1)
            ? [maintenance.CreateNext(utcNow)]
            : [];
        await SaveAsync(additions, [maintenance], completedDraftKey, cancellationToken);
    }

    public async Task StopFutureMaintenanceAsync(
        Guid maintenanceId,
        CancellationToken cancellationToken = default)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        MaintenanceRecord maintenance = data.MaintenanceRecords.SingleOrDefault(item => item.Id == maintenanceId)
            ?? throw new InvalidOperationException("El mantenimiento seleccionado ya no está disponible.");
        if (maintenance.CompletedDate.HasValue)
        {
            throw new InvalidOperationException("Un mantenimiento realizado se conserva como historial y no se elimina.");
        }
        maintenance.MarkDeleted(timeProvider.GetUtcNow().UtcDateTime);
        await repository.SaveAsync([], [maintenance], cancellationToken);
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
        string? completedDraftKey = null,
        Money? defaultSalePrice = null,
        string? description = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        Product product = data.Products.SingleOrDefault(item => item.Id == productId)
            ?? throw new InvalidOperationException("El producto seleccionado ya no está disponible.");
        EnsureUniqueProductName(data, name, productId);
        product.Update(name, category, unitOfMeasure, timeProvider.GetUtcNow().UtcDateTime, defaultSalePrice, description);
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
            Chair chair when chair.AssignedPersonId.HasValue =>
                "No se puede eliminar una silla que está asignada a un trabajador.",
            Product product when data.InventoryMovements.Any(item => item.ProductId == product.Id)
                || data.RestockPlans.Any(item => item.ProductId == product.Id) =>
                "No se puede eliminar el producto porque tiene movimientos o planes mensuales.",
            Obligation obligation when data.ObligationPayments.Any(item => item.ObligationId == obligation.Id) =>
                "No se puede eliminar la obligación porque tiene pagos registrados.",
            Collaborator collaborator when data.MonthlyCloseParticipants.Any(item => item.CollaboratorId == collaborator.Id)
                || data.CollaboratorContributions.Any(item => item.CollaboratorId == collaborator.Id) =>
                "No se puede eliminar el colaborador porque tiene cierres o aportes históricos.",
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
        string? completedDraftKey = null,
        string? description = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        Money debt = WeeklyChargeCalculator.CalculateDebt(
            data.WeeklyCharges.Where(item => item.PersonId == personId),
            data.LocalUsePayments.Where(item => item.PersonId == personId),
            date);
        LocalUsePayment payment = LocalUsePayment.Create(
            personId,
            date,
            amount,
            debt,
            timeProvider.GetUtcNow().UtcDateTime,
            description);
        await SaveAsync([payment], [], completedDraftKey, cancellationToken);
        return payment;
    }

    public async Task<InventoryMovement> RegisterSaleAsync(
        Guid productId,
        DateOnly date,
        Quantity quantity,
        string? description = null,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        Product product = data.Products.SingleOrDefault(item => item.Id == productId)
            ?? throw new InvalidOperationException("El producto seleccionado ya no está disponible.");
        if (!product.IsForSale || !product.DefaultSalePrice.HasValue)
        {
            throw new InvalidOperationException("El producto no está configurado para venta con un precio predeterminado.");
        }

        InventoryMovement[] movements = data.InventoryMovements
            .Where(item => item.ProductId == productId && item.Date <= date)
            .ToArray();
        decimal available = InventoryCalculator.CurrentQuantity(movements);
        InventoryMovement sale = InventoryMovement.Sale(
            productId,
            date,
            quantity,
            product.DefaultSalePrice.Value,
            InventoryCalculator.AverageUnitCost(movements),
            available,
            timeProvider.GetUtcNow().UtcDateTime,
            description);
        await AddInventoryMovementAsync(sale, cancellationToken, completedDraftKey);
        return sale;
    }

    public async Task<InventoryMovement> RegisterPurchaseAsync(
        Guid productId,
        DateOnly date,
        Quantity quantity,
        Money unitCost,
        string? description = null,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        _ = data.Products.SingleOrDefault(item => item.Id == productId)
            ?? throw new InvalidOperationException("El producto seleccionado ya no está disponible.");
        long totalMinorUnits = checked((long)decimal.Round(
            unitCost.MinorUnits * quantity.Value,
            0,
            MidpointRounding.AwayFromZero));
        InventoryMovement purchase = InventoryMovement.Purchase(
            productId,
            date,
            quantity,
            Money.FromMinorUnits(totalMinorUnits),
            timeProvider.GetUtcNow().UtcDateTime,
            description);
        await AddInventoryMovementAsync(purchase, cancellationToken, completedDraftKey);
        return purchase;
    }

    public async Task AddProductWithInitialStockAsync(
        Product product,
        DateOnly entryDate,
        Quantity initialQuantity,
        Money unitCost,
        string? description = null,
        CancellationToken cancellationToken = default,
        string? completedDraftKey = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        EnsureUniqueProductName(data, product.Name, null);
        long totalMinorUnits = checked((long)decimal.Round(
            unitCost.MinorUnits * initialQuantity.Value,
            0,
            MidpointRounding.AwayFromZero));
        InventoryMovement initial = InventoryMovement.Initial(
            product.Id,
            entryDate,
            initialQuantity,
            Money.FromMinorUnits(totalMinorUnits),
            timeProvider.GetUtcNow().UtcDateTime,
            description);
        await SaveAsync([product, initial], [], completedDraftKey, cancellationToken);
    }

    public async Task AddUnofficialExpenseAsync(
        UnofficialExpense expense,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expense);
        await repository.SaveAsync([expense], [], cancellationToken);
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
        string? completedDraftKey = null,
        string? description = null)
    {
        AdministrationData data = await repository.LoadAsync(cancellationToken);
        if (data.MonthlyCloses.Any(close => close.Month == month && close.IsConfirmed))
        {
            throw new InvalidOperationException("El mes ya tiene un cierre confirmado.");
        }

        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        MonthlySummaryResult summary = MonthlySummaryCalculator.Calculate(input, percentage);
        MonthlyClose close = MonthlyClose.Create(month, percentage, summary, utcNow, description);
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
        string? completedDraftKey = null,
        string? description = null)
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
            participant.Id, date, amount, pending, timeProvider.GetUtcNow().UtcDateTime, description);
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

    private static void EnsureUniqueChairName(AdministrationData data, string name, Guid? exceptId)
    {
        string normalized = name.Trim();
        if (data.Chairs.Any(item => item.Id != exceptId
            && string.Equals(item.Name, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Ya existe una silla con ese nombre o número.");
        }
    }
}
