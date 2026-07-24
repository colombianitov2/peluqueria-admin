using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.Activity;
using PeluqueriaAdmin.Domain.Collaborators;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.Finance;
using PeluqueriaAdmin.Domain.Inventory;
using PeluqueriaAdmin.Domain.LocalUse;
using PeluqueriaAdmin.Domain.Maintenance;
using PeluqueriaAdmin.Domain.Obligations;
using PeluqueriaAdmin.Domain.Settings;
using PeluqueriaAdmin.Infrastructure.Persistence;

namespace PeluqueriaAdmin.Infrastructure.Administration;

public sealed class EfAdministrationRepository(IDbContextFactory<PeluqueriaDbContext> contextFactory)
    : IAdministrationRepository
{
    public async Task<AdministrationData> LoadAsync(CancellationToken cancellationToken = default)
    {
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return new AdministrationData(
            await context.LocalUsePeople.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken),
            await context.WeeklyRates.AsNoTracking().OrderBy(item => item.EffectiveFrom).ThenBy(item => item.CreatedUtc).ToListAsync(cancellationToken),
            await context.WeeklyCharges.AsNoTracking().OrderBy(item => item.PeriodStart).ToListAsync(cancellationToken),
            await context.LocalUsePayments.AsNoTracking().OrderBy(item => item.PaymentDate).ToListAsync(cancellationToken),
            await context.Products.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken),
            await context.InventoryMovements.AsNoTracking().OrderBy(item => item.Date).ThenBy(item => item.CreatedUtc).ToListAsync(cancellationToken),
            await context.RestockPlans.AsNoTracking().ToListAsync(cancellationToken),
            await context.FinancialEntries.AsNoTracking().OrderBy(item => item.Date).ToListAsync(cancellationToken),
            await context.Obligations.AsNoTracking().OrderBy(item => item.DueDate).ToListAsync(cancellationToken),
            await context.ObligationPayments.AsNoTracking().OrderBy(item => item.Date).ToListAsync(cancellationToken),
            await context.MaintenanceRecords.AsNoTracking().OrderBy(item => item.ScheduledDate).ToListAsync(cancellationToken),
            await context.Collaborators.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken),
            await context.MonthlyCloses.AsNoTracking().ToListAsync(cancellationToken),
            await context.MonthlyCloseParticipants.AsNoTracking().ToListAsync(cancellationToken),
            await context.DistributionPayments.AsNoTracking().OrderBy(item => item.Date).ToListAsync(cancellationToken),
            await context.Chairs.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken),
            await context.ActivityRecords.AsNoTracking().OrderByDescending(item => item.OccurredUtc).ToListAsync(cancellationToken),
            await context.UnofficialExpenses.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken),
            await context.CollaboratorContributions.AsNoTracking().OrderBy(item => item.Date).ThenBy(item => item.CreatedUtc).ToListAsync(cancellationToken),
            await context.CollaboratorContributionEvents.AsNoTracking().OrderBy(item => item.OccurredUtc).ToListAsync(cancellationToken),
            await context.FinancialReserves.AsNoTracking().OrderBy(item => item.DueDate).ToListAsync(cancellationToken),
            await context.FinancialCloseExclusions.AsNoTracking().ToListAsync(cancellationToken),
            await context.MonthlyPurchaseItems.AsNoTracking().OrderBy(item => item.Month).ToListAsync(cancellationToken),
            await context.Loans.AsNoTracking().OrderBy(item => item.NextDueDate).ToListAsync(cancellationToken),
            await context.LoanInstallments.AsNoTracking().OrderBy(item => item.DueDate).ThenBy(item => item.Number).ToListAsync(cancellationToken),
            await context.LoanPayments.AsNoTracking().OrderBy(item => item.Date).ToListAsync(cancellationToken),
            await context.AnnualCloses.AsNoTracking().OrderBy(item => item.Year).ToListAsync(cancellationToken),
            await context.AnnualCarryovers.AsNoTracking().OrderBy(item => item.TargetYear).ToListAsync(cancellationToken));
    }

    public async Task SaveAsync(
        IReadOnlyCollection<AuditableEntity> additions,
        IReadOnlyCollection<AuditableEntity> updates,
        CancellationToken cancellationToken = default)
    {
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.AddRange(additions.Concat(BuildActivityRecords(additions, updates)));
        context.UpdateRange(updates);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveCompletingDraftAsync(
        IReadOnlyCollection<AuditableEntity> additions,
        IReadOnlyCollection<AuditableEntity> updates,
        string completedDraftKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(completedDraftKey);
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.AddRange(additions.Concat(BuildActivityRecords(additions, updates)));
        context.UpdateRange(updates);
        await context.FormDrafts.Where(item => item.Key == completedDraftKey)
            .ExecuteDeleteAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveSettingsAndRateAsync(
        GeneralSettings settings,
        WeeklyRate? newRate,
        CancellationToken cancellationToken = default)
    {
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await AddSettingsActivityIfChangedAsync(context, settings, cancellationToken);
        context.Settings.Update(settings);
        if (newRate is not null)
        {
            context.WeeklyRates.Add(newRate);
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveSettingsAndRateCompletingDraftAsync(
        GeneralSettings settings,
        WeeklyRate? newRate,
        string completedDraftKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(completedDraftKey);
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await AddSettingsActivityIfChangedAsync(context, settings, cancellationToken);
        context.Settings.Update(settings);
        if (newRate is not null) context.WeeklyRates.Add(newRate);
        await context.FormDrafts.Where(item => item.Key == completedDraftKey).ExecuteDeleteAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task AddSettingsActivityIfChangedAsync(
        PeluqueriaDbContext context,
        GeneralSettings settings,
        CancellationToken cancellationToken)
    {
        GeneralSettings? previous = await context.Settings.AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        bool changed = previous is null
            || previous.WeeklyUsageFee != settings.WeeklyUsageFee
            || previous.CollaboratorProfit != settings.CollaboratorProfit
            || !string.Equals(previous.ExportDirectory, settings.ExportDirectory, StringComparison.Ordinal);
        if (!changed) return;

        context.ActivityRecords.Add(ActivityRecord.Create(
            DateOnly.FromDateTime(settings.UpdatedUtc.ToLocalTime()),
            "Ajustes",
            "Edición",
            "Ajustes generales actualizados",
            Guid.Empty,
            "Cambio válido consolidado por autoguardado; no se almacenan rutas ni valores sensibles.",
            settings.UpdatedUtc));
    }

    private static IReadOnlyList<ActivityRecord> BuildActivityRecords(
        IReadOnlyCollection<AuditableEntity> additions,
        IReadOnlyCollection<AuditableEntity> updates)
    {
        HashSet<Guid> explicitlyAuditedEntityIds = additions
            .OfType<ActivityRecord>()
            .Where(item => item.EntityId.HasValue)
            .Select(item => item.EntityId!.Value)
            .ToHashSet();
        return additions.Select(item => CreateActivity(item, true))
            .Concat(updates
                .Where(item => !explicitlyAuditedEntityIds.Contains(item.Id))
                .Select(item => CreateActivity(item, false)))
            .Where(item => item is not null)
            .Cast<ActivityRecord>()
            .ToArray();
    }

    private static ActivityRecord? CreateActivity(AuditableEntity entity, bool isAddition)
    {
        if (entity is ActivityRecord or WeeklyCharge or WeeklyRate or MonthlyCloseParticipant
            or CollaboratorContribution or CollaboratorContributionEvent or LoanInstallment
            or AnnualCarryover)
        {
            return null;
        }

        string action = entity.IsDeleted ? "Eliminación" : isAddition ? "Creación" : "Edición";
        (string module, string summary, DateOnly date, string? description) = entity switch
        {
            LocalUsePerson item => ("Uso del local", item.Name, item.EntryDate, item.Description),
            Chair item => ("Uso del local", item.Name,
                isAddition ? item.CreationDate : DateOnly.FromDateTime(item.UpdatedUtc.ToLocalTime()), item.Description),
            LocalUsePayment item => ("Uso del local", "Pago por uso del local",
                item.PaymentDate, item.Description),
            Product item => ("Inventario", item.Name,
                DateOnly.FromDateTime(item.UpdatedUtc.ToLocalTime()), item.Description),
            InventoryMovement item => (item.Type == InventoryMovementType.Sale ? "Ventas" : "Inventario",
                item.Type switch
                {
                    InventoryMovementType.InitialStock => "Existencia inicial",
                    InventoryMovementType.Purchase => "Compra",
                    InventoryMovementType.Sale => "Venta",
                    InventoryMovementType.InternalConsumption => "Consumo",
                    InventoryMovementType.PhysicalCountAdjustment => "Conteo físico",
                    _ => "Movimiento de inventario",
                }, item.Date, item.Description),
            FinancialEntry item => (item.Type switch
            {
                FinancialEntryType.OtherIncome => "Otros ingresos",
                FinancialEntryType.Expense => "Gastos",
                _ => "Imprevistos",
            }, item.Concept, item.Date, item.Description),
            Obligation item => ("Obligaciones", item.Name, item.DueDate, item.Description),
            ObligationPayment item => ("Obligaciones", "Pago de obligación", item.Date, item.Description),
            MaintenanceRecord item => ("Mantenimiento", $"{item.Asset}: {item.MaintenanceType}",
                item.CompletedDate ?? item.ScheduledDate, item.Description),
            Collaborator item => ("Colaboradores", item.Name, item.StartDate, item.Description),
            CollaboratorContribution item => ("Colaboradores", "Aporte de capital",
                item.Date, item.Description),
            MonthlyClose item => ("Resumen mensual", $"Cierre {item.Month}",
                item.Month.LastDay, item.Description),
            DistributionPayment item => ("Colaboradores", "Pago a colaborador", item.Date, item.Description),
            UnofficialExpense item => ("Ajustes", item.Name, item.EffectiveFrom, item.Description),
            MonthlyPurchaseItem item => ("Inventario", "Lista mensual de compra", item.Month.FirstDay, item.Description),
            Loan item => ("Obligaciones", item.Name, item.StartDate, item.Description),
            LoanPayment item => ("Obligaciones", "Pago de préstamo", item.Date, item.Description),
            FinancialReserve item => ("Resumen mensual", $"Reserva: {item.Name}", item.DueDate, null),
            FinancialCloseExclusion item => ("Resumen mensual", "Exclusión de cierre", item.Month.LastDay, item.Reason),
            AnnualClose item => ("Balance anual", $"Cierre {item.Year}", new DateOnly(item.Year, 12, 31), null),
            _ => default,
        };

        if (string.IsNullOrWhiteSpace(module))
        {
            return null;
        }

        if (entity is LocalUsePayment or ObligationPayment or DistributionPayment or LoanPayment)
        {
            action = isAddition ? "Pago" : action;
        }
        else if (entity is InventoryMovement movement)
        {
            action = movement.Type switch
            {
                InventoryMovementType.InitialStock => "Creación",
                InventoryMovementType.Sale => "Venta",
                InventoryMovementType.Purchase => "Compra",
                InventoryMovementType.InternalConsumption => "Consumo",
                InventoryMovementType.PhysicalCountAdjustment => "Ajuste de inventario",
                _ => action,
            };
        }
        else if (entity is MonthlyClose close)
        {
            action = isAddition ? "Cierre" : close.IsConfirmed ? "Edición" : "Reapertura";
        }
        else if (entity is UnofficialExpense)
        {
            action = "Cambio de ajustes";
        }

        DateTime utcNow = entity.UpdatedUtc.Kind == DateTimeKind.Utc
            ? entity.UpdatedUtc
            : DateTime.SpecifyKind(entity.UpdatedUtc, DateTimeKind.Utc);
        Guid entityId = entity is CollaboratorContribution contribution
            ? contribution.CollaboratorId
            : entity.Id;
        return ActivityRecord.Create(date, module, action, summary, entityId, description, utcNow);
    }
}
