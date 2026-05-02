using FinTrackAI.Domain.Entities;

namespace FinTrackAI.Domain.Interfaces.Repositories;

public interface ILancamentoRepository
{
    Task<IReadOnlyList<Lancamento>> GetByPeriodoAsync(
        long dataInicioMs,
        long dataFimExclusivoMs,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<(string NomeCategoria, double Total)>> GetGastosDespesasPorCategoriaAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken);

    Task<(double TotalReceitas, double TotalDespesas, double Saldo)> GetResumoMensalAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<(int FormaPagamento, double Total)>> GetGastosPorFormaPagamentoAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken);

    Task<Lancamento?> GetMaiorDespesaAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken);
}
