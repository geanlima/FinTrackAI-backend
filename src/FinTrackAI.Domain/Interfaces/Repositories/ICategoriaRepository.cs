using FinTrackAI.Domain.Entities;

namespace FinTrackAI.Domain.Interfaces.Repositories;

public interface ICategoriaRepository
{
    Task<IReadOnlyList<Categoria>> GetAllAsync(CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<int, string>> GetNomesPorIdAsync(CancellationToken cancellationToken);
}
