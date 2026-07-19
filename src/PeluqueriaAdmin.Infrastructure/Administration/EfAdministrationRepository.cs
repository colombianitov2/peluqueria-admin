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
            await context.UnofficialExpenses.AsNoTracking().OrderBy(item => item.Name).ToListAsync(cancellationToken));
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
        context.Settings.Update(settings);
        if (newRate is not null) context.WeeklyRates.Add(newRate);
        await context.FormDrafts.Where(item => item.Key == completedDraftKey).ExecuteDeleteAsync(cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static IReadOnlyList<ActivityRecord> BuildActivityRecords(
        IReadOnlyCollection<AuditableEntity> additions,
        IReadOnlyCollection<AuditableEntity> updates)
    {
        return additions.Select(item => CreateActivity(item, true))
            .Concat(updates.Select(item => CreateActivity(item, false)))
            .Where(item => item is not null)
            .Cast<ActivityRecord>()
            .ToArray();
    }

    private static ActivityRecord? CreateActivity(AuditableEntity entity, bool isAddition)
    {
        if (entity is ActivityRecord or WeeklyCharge or WeeklyRate or MonthlyCloseParticipant)
        {
            return null;
        }

        string action = entity.IsDeleted ? "Eliminación" : isAddition ? "Alta" : "Edición";
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
            MonthlyClose item => ("Resumen mensual", $"Cierre {item.Month}",
                item.Month.LastDay, item.Description),
            DistributionPayment item => ("Colaboradores", "Pago a colaborador", item.Date, item.Description),
            UnofficialExpense item => ("Ajustes", item.Name, item.EffectiveFrom, item.Description),
            _ => default,
        };

        if (string.IsNullOrWhiteSpace(module))
        {
            return null;
        }

        if (entity is LocalUsePayment or ObligationPayment or DistributionPayment)
        {
            action = isAddition ? "Pago" : action;
        }
        else if (entity is InventoryMovement movement)
        {
            action = movement.Type switch
            {
                InventoryMovementType.Sale => "Venta",
                InventoryMovementType.Purchase => "Compra",
                _ => action,
            };
        }
        else if (entity is Chair && !isAddition && !entity.IsDeleted)
        {
            action = "Asignación o edición";
        }
        else if (entity is MonthlyClose && isAddition)
        {
            action = "Cierre";
        }

        DateTime utcNow = entity.UpdatedUtc.Kind == DateTimeKind.Utc
            ? entity.UpdatedUtc
            : DateTime.SpecifyKind(entity.UpdatedUtc, DateTimeKind.Utc);
        return ActivityRecord.Create(date, module, action, summary, entity.Id, description, utcNow);
    }
}
