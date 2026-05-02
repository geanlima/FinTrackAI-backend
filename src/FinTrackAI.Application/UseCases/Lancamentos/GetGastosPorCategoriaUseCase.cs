using FinTrackAI.Application.DTOs;
using FinTrackAI.Domain.Interfaces.Repositories;

namespace FinTrackAI.Application.UseCases.Lancamentos;

public sealed class GetGastosPorCategoriaUseCase
{
    private readonly ILancamentoRepository _lancamentos;

    public GetGastosPorCategoriaUseCase(ILancamentoRepository lancamentos)
    {
        _lancamentos = lancamentos;
    }

    public async Task<IReadOnlyList<GastoCategoriaDto>> ExecuteAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<(string NomeCategoria, double Total)> agregados =
            await _lancamentos.GetGastosDespesasPorCategoriaAsync(mes, ano, cancellationToken);
        List<GastoCategoriaDto> lista = new List<GastoCategoriaDto>(agregados.Count);
        foreach ((string nome, double total) in agregados)
        {
            lista.Add(new GastoCategoriaDto { NomeCategoria = nome, Total = total });
        }

        return lista;
    }
}
