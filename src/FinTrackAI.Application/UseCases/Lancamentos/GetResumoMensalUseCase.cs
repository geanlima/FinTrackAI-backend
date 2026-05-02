using FinTrackAI.Application.DTOs;
using FinTrackAI.Domain.Interfaces.Repositories;

namespace FinTrackAI.Application.UseCases.Lancamentos;

public sealed class GetResumoMensalUseCase
{
    private readonly ILancamentoRepository _lancamentos;

    public GetResumoMensalUseCase(ILancamentoRepository lancamentos)
    {
        _lancamentos = lancamentos;
    }

    public async Task<ResumoFinanceiroDto> ExecuteAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken)
    {
        (double receitas, double despesas, double saldo) =
            await _lancamentos.GetResumoMensalAsync(mes, ano, cancellationToken);
        return new ResumoFinanceiroDto
        {
            Mes = mes,
            Ano = ano,
            TotalReceitas = receitas,
            TotalDespesas = despesas,
            Saldo = saldo,
        };
    }
}
