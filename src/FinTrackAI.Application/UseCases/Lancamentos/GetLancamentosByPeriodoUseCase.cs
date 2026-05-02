using FinTrackAI.Application.DTOs;
using FinTrackAI.Application.Helpers;
using FinTrackAI.Domain.Entities;
using FinTrackAI.Domain.Interfaces.Repositories;

namespace FinTrackAI.Application.UseCases.Lancamentos;

public sealed class GetLancamentosByPeriodoUseCase
{
    private const int TipoDespesa = 1;
    private const int TipoReceita = 2;

    private readonly ILancamentoRepository _lancamentos;
    private readonly ICategoriaRepository _categorias;

    public GetLancamentosByPeriodoUseCase(
        ILancamentoRepository lancamentos,
        ICategoriaRepository categorias)
    {
        _lancamentos = lancamentos;
        _categorias = categorias;
    }

    public async Task<IReadOnlyList<LancamentoDto>> ExecuteAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken)
    {
        (long inicioMs, long fimExclusivoMs) = DateTimeHelper.IntervaloMesLocal(mes, ano);
        IReadOnlyList<Lancamento> lista = await _lancamentos.GetByPeriodoAsync(
            inicioMs,
            fimExclusivoMs,
            cancellationToken);
        IReadOnlyDictionary<int, string> nomesCategoria =
            await _categorias.GetNomesPorIdAsync(cancellationToken);

        List<LancamentoDto> resultado = new List<LancamentoDto>(lista.Count);
        foreach (Lancamento l in lista)
        {
            if (l.TipoMovimento != TipoDespesa && l.TipoMovimento != TipoReceita)
            {
                continue;
            }

            string? nomeCat = null;
            int idCategoria = l.IdCategoriaPersonalizada ?? 0;
            if (idCategoria > 0 && nomesCategoria.TryGetValue(idCategoria, out string? n))
            {
                nomeCat = n;
            }

            resultado.Add(new LancamentoDto
            {
                Id = l.Id,
                Valor = l.Valor,
                Descricao = l.Descricao,
                FormaPagamento = l.FormaPagamento,
                DataHora = l.DataHora,
                DataHoraFormatada = l.DataHoraFormatada,
                TipoMovimento = l.TipoMovimento,
                IdCategoriaPersonalizada = l.IdCategoriaPersonalizada,
                NomeCategoria = nomeCat,
                IdSubcategoriaPersonalizada = l.IdSubcategoriaPersonalizada,
                IdCartao = l.IdCartao,
                IdConta = l.IdConta,
                Pago = l.Pago,
            });
        }

        return resultado;
    }
}
