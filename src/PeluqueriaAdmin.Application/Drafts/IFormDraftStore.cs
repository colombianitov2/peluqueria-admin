using PeluqueriaAdmin.Domain.Drafts;

namespace PeluqueriaAdmin.Application.Drafts;

public interface IFormDraftStore
{
    Task<IReadOnlyList<FormDraft>> LoadAllAsync(CancellationToken cancellationToken = default);

    Task<FormDraft?> FindAsync(string key, CancellationToken cancellationToken = default);

    Task UpsertAsync(FormDraft draft, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
