using PeluqueriaAdmin.Domain.Notes;

namespace PeluqueriaAdmin.Application.Notes;

public interface INoteRepository
{
    Task<AppNote?> GetAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppNote note, CancellationToken cancellationToken = default);
}
