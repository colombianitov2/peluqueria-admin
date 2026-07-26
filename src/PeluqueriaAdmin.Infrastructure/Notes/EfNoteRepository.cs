using Microsoft.EntityFrameworkCore;
using PeluqueriaAdmin.Application.Notes;
using PeluqueriaAdmin.Domain.Notes;
using PeluqueriaAdmin.Infrastructure.Persistence;

namespace PeluqueriaAdmin.Infrastructure.Notes;

public sealed class EfNoteRepository(IDbContextFactory<PeluqueriaDbContext> contextFactory) : INoteRepository
{
    public async Task<AppNote?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.Notes.SingleOrDefaultAsync(item => item.Id == AppNote.SingletonId, cancellationToken);
    }

    public async Task SaveAsync(AppNote note, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(note);
        await using PeluqueriaDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken);
        AppNote? stored = await context.Notes.SingleOrDefaultAsync(
            item => item.Id == AppNote.SingletonId, cancellationToken);
        if (stored is null)
        {
            context.Notes.Add(note);
        }
        else
        {
            context.Entry(stored).CurrentValues.SetValues(note);
        }
        await context.SaveChangesAsync(cancellationToken);
    }
}
