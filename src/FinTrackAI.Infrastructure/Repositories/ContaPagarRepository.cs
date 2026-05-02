using FinTrackAI.Domain.Entities;
using FinTrackAI.Domain.Interfaces.Repositories;
using FinTrackAI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinTrackAI.Infrastructure.Repositories;

public sealed class ContaPagarRepository : IContaPagarRepository
{
    private readonly FinTrackDbContext _db;

    public ContaPagarRepository(FinTrackDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ContaPagar>> GetPendentesAsync(CancellationToken cancellationToken)
    {
        List<ContaPagar> lista = await _db.ContasPagar
            .AsNoTracking()
            .Where(c => c.Pago != 1)
            .OrderBy(c => c.DataVencimento)
            .ToListAsync(cancellationToken);
        return lista;
    }
}
