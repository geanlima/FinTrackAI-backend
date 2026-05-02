using FinTrackAI.Application.DTOs;
using FinTrackAI.Domain.Entities;
using FinTrackAI.Domain.Interfaces.Repositories;

namespace FinTrackAI.Application.UseCases.ContasPagar;

public sealed class GetContasPagarPendentesUseCase
{
    private readonly IContaPagarRepository _contasPagar;

    public GetContasPagarPendentesUseCase(IContaPagarRepository contasPagar)
    {
        _contasPagar = contasPagar;
    }

    public async Task<IReadOnlyList<ContaPagarDto>> ExecuteAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ContaPagar> pendentes = await _contasPagar.GetPendentesAsync(cancellationToken);
        List<ContaPagarDto> lista = new List<ContaPagarDto>(pendentes.Count);
        foreach (ContaPagar c in pendentes)
        {
            lista.Add(new ContaPagarDto
            {
                Id = c.Id,
                Descricao = c.Descricao,
                Valor = c.Valor,
                DataVencimento = c.DataVencimento,
                FormaPagamento = c.FormaPagamento,
                IdCartao = c.IdCartao,
                IdConta = c.IdConta,
            });
        }

        return lista;
    }
}
