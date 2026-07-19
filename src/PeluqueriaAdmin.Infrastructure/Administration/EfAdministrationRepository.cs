using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Application.Administration;
using PeluqueriaAdmin.Domain.Common;
using PeluqueriaAdmin.Domain.LocalUse;
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
            await context.DistributionPayments.AsNoTracking().OrderBy(item => item.Date).ToListAsync(cancellationToken));
    }

    public async Task SaveAsync(
        IReadOnlyCollection<AuditableEntity> additions,
        IReadOnlyCollection<AuditableEntity> updates,
        CancellationToken cancellationToken = default)
    {
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.AddRange(additions);
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
        context.AddRange(additions);
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
}
