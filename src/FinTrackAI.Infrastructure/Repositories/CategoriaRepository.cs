using FinTrackAI.Domain.Entities;
using FinTrackAI.Domain.Interfaces.Repositories;
using FinTrackAI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FinTrackAI.Infrastructure.Repositories;

public sealed class CategoriaRepository : ICategoriaRepository
{
    private readonly FinTrackDbContext _db;

    public CategoriaRepository(FinTrackDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Categoria>> GetAllAsync(CancellationToken cancellationToken)
    {
        List<Categoria> lista = await _db.CategoriasPersonalizadas
            .AsNoTracking()
            .OrderBy(c => c.Nome)
            .ToListAsync(cancellationToken);
        return lista;
    }

    public async Task<IReadOnlyDictionary<int, string>> GetNomesPorIdAsync(CancellationToken cancellationToken)
    {
        return await _db.CategoriasPersonalizadas
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Nome ?? string.Empty, cancellationToken);
    }
}
