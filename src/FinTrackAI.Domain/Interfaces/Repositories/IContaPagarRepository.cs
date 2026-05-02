using FinTrackAI.Domain.Entities;

namespace FinTrackAI.Domain.Interfaces.Repositories;

public interface IContaPagarRepository
{
    Task<IReadOnlyList<ContaPagar>> GetPendentesAsync(CancellationToken cancellationToken);
}
