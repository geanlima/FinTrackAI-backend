using System.Text.Json;
using FinTrackAI.Application.Helpers;
using FinTrackAI.Domain.Entities;
using FinTrackAI.Domain.Interfaces.Repositories;

namespace FinTrackAI.Application.Services;

public sealed class FinanceiroQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly ILancamentoRepository _lancamentos;
    private readonly IContaPagarRepository _contasPagar;

    public FinanceiroQueryService(
        ILancamentoRepository lancamentos,
        IContaPagarRepository contasPagar)
    {
        _lancamentos = lancamentos;
        _contasPagar = contasPagar;
    }

    public async Task<string> BuscarLancamentosPorPeriodoAsync(
        long dataInicioMs,
        long dataFimMs,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Lancamento> lista = await _lancamentos.GetByPeriodoAsync(
            dataInicioMs,
            dataFimMs,
            cancellationToken);
        List<object> projecao = new List<object>(lista.Count);
        foreach (Lancamento l in lista)
        {
            projecao.Add(new
            {
                l.Id,
                l.Valor,
                l.Descricao,
                l.FormaPagamento,
                l.DataHora,
                DataHoraFormatada = l.DataHoraFormatada,
                l.TipoMovimento,
                l.IdCategoriaPersonalizada,
                l.IdSubcategoriaPersonalizada,
                l.IdCartao,
                l.IdConta,
                l.Pago,
            });
        }

        return JsonSerializer.Serialize(projecao, JsonOptions);
    }

    public async Task<string> CalcularTotalPorCategoriaAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<(string NomeCategoria, double Total)> agregados =
            await _lancamentos.GetGastosDespesasPorCategoriaAsync(mes, ano, cancellationToken);
        return JsonSerializer.Serialize(
            new
            {
                mes,
                ano,
                periodo = DateTimeHelper.FormatarMesAno(mes, ano),
                categorias = agregados.Select(a => new { nome = a.NomeCategoria, total = a.Total }),
            },
            JsonOptions);
    }

    public async Task<string> GetResumoMensalAsync(int mes, int ano, CancellationToken cancellationToken)
    {
        (double receitas, double despesas, double saldo) =
            await _lancamentos.GetResumoMensalAsync(mes, ano, cancellationToken);
        return JsonSerializer.Serialize(
            new
            {
                mes,
                ano,
                periodo = DateTimeHelper.FormatarMesAno(mes, ano),
                totalReceitas = receitas,
                totalDespesas = despesas,
                saldo,
                totalReceitasFormatado = DateTimeHelper.FormatarMoedaBr(receitas),
                totalDespesasFormatado = DateTimeHelper.FormatarMoedaBr(despesas),
                saldoFormatado = DateTimeHelper.FormatarMoedaBr(saldo),
            },
            JsonOptions);
    }

    public async Task<string> GetContasPagarPendentesAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ContaPagar> lista = await _contasPagar.GetPendentesAsync(cancellationToken);
        List<object> projecao = new List<object>(lista.Count);
        foreach (ContaPagar c in lista)
        {
            projecao.Add(new
            {
                c.Id,
                c.Descricao,
                c.Valor,
                c.DataVencimento,
                DataVencimentoFormatada = c.DataVencimento > 0
                    ? DateTimeHelper.FromTimestampMs(c.DataVencimento).ToString("dd/MM/yyyy")
                    : null,
                c.FormaPagamento,
                c.IdCartao,
                c.IdConta,
            });
        }

        return JsonSerializer.Serialize(projecao, JsonOptions);
    }

    public async Task<string> GetGastosPorFormaPagamentoAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<(int FormaPagamento, double Total)> agregados =
            await _lancamentos.GetGastosPorFormaPagamentoAsync(mes, ano, cancellationToken);
        return JsonSerializer.Serialize(
            new
            {
                mes,
                ano,
                periodo = DateTimeHelper.FormatarMesAno(mes, ano),
                porFormaPagamento = agregados.Select(a => new
                {
                    formaPagamento = a.FormaPagamento,
                    total = a.Total,
                    totalFormatado = DateTimeHelper.FormatarMoedaBr(a.Total),
                }),
            },
            JsonOptions);
    }

    public async Task<string> GetMaiorGastoAsync(int mes, int ano, CancellationToken cancellationToken)
    {
        Lancamento? maior = await _lancamentos.GetMaiorDespesaAsync(mes, ano, cancellationToken);
        if (maior == null)
        {
            return JsonSerializer.Serialize(
                new { mes, ano, mensagem = "Nenhuma despesa encontrada no período." },
                JsonOptions);
        }

        object payload = new
        {
            mes,
            ano,
            maior.Id,
            maior.Valor,
            maior.Descricao,
            maior.DataHora,
            DataHoraFormatada = maior.DataHoraFormatada,
            maior.FormaPagamento,
            maior.IdCategoriaPersonalizada,
            valorFormatado = DateTimeHelper.FormatarMoedaBr(maior.Valor),
        };
        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
