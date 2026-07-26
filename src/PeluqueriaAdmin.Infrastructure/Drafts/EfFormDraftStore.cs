using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Application.Drafts;
using PeluqueriaAdmin.Domain.Drafts;
using PeluqueriaAdmin.Infrastructure.Persistence;

namespace PeluqueriaAdmin.Infrastructure.Drafts;

public sealed class EfFormDraftStore(IDbContextFactory<PeluqueriaDbContext> contextFactory)
    : IFormDraftStore
{
    public async Task<IReadOnlyList<FormDraft>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.FormDrafts.AsNoTracking()
            .OrderByDescending(item => item.UpdatedUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<FormDraft?> FindAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.FormDrafts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Key == key, cancellationToken);
    }

    public async Task UpsertAsync(FormDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        FormDraft? existing = await context.FormDrafts
            .SingleOrDefaultAsync(item => item.Key == draft.Key, cancellationToken);
        if (existing is null)
        {
            context.FormDrafts.Add(draft);
        }
        else
        {
            existing.UpdatePayload(draft.PayloadJson, draft.UpdatedUtc);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        FormDraft? draft = await context.FormDrafts.SingleOrDefaultAsync(item => item.Key == key, cancellationToken);
        if (draft is null)
        {
            return;
        }

        context.FormDrafts.Remove(draft);
        await context.SaveChangesAsync(cancellationToken);
    }
}
