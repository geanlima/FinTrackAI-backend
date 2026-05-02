using FinTrackAI.Application.DTOs;
using FinTrackAI.Domain.Entities;
using FinTrackAI.Domain.Interfaces.Repositories;

namespace FinTrackAI.Application.UseCases.Categorias;

public sealed class GetCategoriasUseCase
{
    private readonly ICategoriaRepository _categorias;

    public GetCategoriasUseCase(ICategoriaRepository categorias)
    {
        _categorias = categorias;
    }

    public async Task<IReadOnlyList<CategoriaDto>> ExecuteAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<Categoria> lista = await _categorias.GetAllAsync(cancellationToken);
        List<CategoriaDto> resultado = new List<CategoriaDto>(lista.Count);
        foreach (Categoria c in lista)
        {
            resultado.Add(new CategoriaDto
            {
                Id = c.Id,
                Nome = c.Nome,
                TipoMovimento = c.TipoMovimento,
                Cor = c.Cor,
            });
        }

        return resultado;
    }
}
