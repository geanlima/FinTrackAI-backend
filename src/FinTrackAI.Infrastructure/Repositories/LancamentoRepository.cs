using FinTrackAI.Domain.Entities;
using FinTrackAI.Domain.Interfaces.Repositories;
using FinTrackAI.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinTrackAI.Infrastructure.Repositories;

/// <summary>
/// Agregações usam <c>pagamento_fatura == 0</c> no LINQ (traduzível pelo EF). Valores != 0 são pagamento de fatura no Vox.
/// </summary>
public sealed class LancamentoRepository : ILancamentoRepository
{
    private const int TipoDespesa = 1;
    private const int TipoReceita = 2;
    private const int PagoSim = 1;

    private readonly FinTrackDbContext _db;
    private readonly ILogger<LancamentoRepository> _logger;

    public LancamentoRepository(FinTrackDbContext db, ILogger<LancamentoRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Lancamento>> GetByPeriodoAsync(
        long dataInicioMs,
        long dataFimExclusivoMs,
        CancellationToken cancellationToken)
    {
        List<Lancamento> lista = await _db.Lancamentos
            .AsNoTracking()
            .Where(l => l.DataHora >= dataInicioMs && l.DataHora < dataFimExclusivoMs)
            .OrderByDescending(l => l.DataHora)
            .ToListAsync(cancellationToken);
        return lista;
    }

    public async Task<IReadOnlyList<(string NomeCategoria, double Total)>> GetGastosDespesasPorCategoriaAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken)
    {
        (long inicioMs, long fimInclusivoMs) = IntervaloMes(mes, ano);
        List<(int IdCat, double Total)> grupos = await _db.Lancamentos
            .AsNoTracking()
            .Where(l =>
                l.DataHora >= inicioMs
                && l.DataHora <= fimInclusivoMs
                && l.TipoMovimento == TipoDespesa
                && l.Pago == PagoSim
                && l.PagamentoFatura == 0)
            .GroupBy(l => l.IdCategoriaPersonalizada ?? 0)
            .Select(g => new ValueTuple<int, double>(g.Key, g.Sum(x => (double)x.Valor)))
            .ToListAsync(cancellationToken);

        Dictionary<int, string> nomes = await _db.CategoriasPersonalizadas
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Id, c => c.Nome ?? "Sem nome", cancellationToken);

        List<(string NomeCategoria, double Total)> resultado =
            new List<(string NomeCategoria, double Total)>(grupos.Count);
        foreach ((int idCat, double total) in grupos)
        {
            string nome = idCat <= 0 || !nomes.TryGetValue(idCat, out string? n)
                ? "Sem categoria"
                : n;
            resultado.Add((nome, total));
        }

        resultado.Sort((a, b) => b.Total.CompareTo(a.Total));
        return resultado;
    }

    public async Task<(double TotalReceitas, double TotalDespesas, double Saldo)> GetResumoMensalAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken)
    {
        (long inicioMs, long fimInclusivoMs) = IntervaloMes(mes, ano);

        // Receitas: prioriza cadastro "Minha Renda" (fontes ativas). Se não houver valor cadastrado,
        // usa lançamentos de receita no mês (pagos, exclui pagamento de fatura).
        double receitasFontes = 0d;
        try
        {
            receitasFontes = await _db.FontesRenda
                .AsNoTracking()
                .Where(f => f.Ativa == null || f.Ativa != 0)
                .SumAsync(f => (double?)f.ValorBase, cancellationToken) ?? 0d;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível ler fontes_renda; receitas virão só de lançamentos.");
        }

        double receitasLancamentos = await _db.Lancamentos
            .AsNoTracking()
            .Where(l =>
                l.DataHora >= inicioMs
                && l.DataHora <= fimInclusivoMs
                && l.TipoMovimento == TipoReceita
                && l.Pago == PagoSim
                && l.PagamentoFatura == 0)
            .SumAsync(l => (double?)l.Valor, cancellationToken) ?? 0d;

        double receitas = receitasFontes > 0 ? receitasFontes : receitasLancamentos;

        // Gasto do mês: despesas pagas, fora de pagamento de fatura, no intervalo do mês (inclusive).
        double despesas = await _db.Lancamentos
            .AsNoTracking()
            .Where(l =>
                l.DataHora >= inicioMs
                && l.DataHora <= fimInclusivoMs
                && l.TipoMovimento == TipoDespesa
                && l.Pago == PagoSim
                && l.PagamentoFatura == 0)
            .SumAsync(l => (double?)l.Valor, cancellationToken) ?? 0d;
        return (receitas, despesas, receitas - despesas);
    }

    public async Task<IReadOnlyList<(int FormaPagamento, double Total)>> GetGastosPorFormaPagamentoAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken)
    {
        (long inicioMs, long fimInclusivoMs) = IntervaloMes(mes, ano);
        List<(int FormaPagamento, double Total)> lista = await _db.Lancamentos
            .AsNoTracking()
            .Where(l =>
                l.DataHora >= inicioMs
                && l.DataHora <= fimInclusivoMs
                && l.TipoMovimento == TipoDespesa
                && l.Pago == PagoSim
                && l.PagamentoFatura == 0)
            .GroupBy(l => l.FormaPagamento ?? 0)
            .Select(g => new ValueTuple<int, double>(g.Key, g.Sum(x => (double)x.Valor)))
            .ToListAsync(cancellationToken);
        lista.Sort((a, b) => b.Total.CompareTo(a.Total));
        return lista;
    }

    public async Task<Lancamento?> GetMaiorDespesaAsync(
        int mes,
        int ano,
        CancellationToken cancellationToken)
    {
        (long inicioMs, long fimInclusivoMs) = IntervaloMes(mes, ano);
        return await _db.Lancamentos
            .AsNoTracking()
            .Where(l =>
                l.DataHora >= inicioMs
                && l.DataHora <= fimInclusivoMs
                && l.TipoMovimento == TipoDespesa
                && l.Pago == PagoSim
                && l.PagamentoFatura == 0)
            .OrderByDescending(l => l.Valor)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Início do 1º dia 00:00:00 e fim do último dia 23:59:59 (local), em ms — alinhado ao app Vox.
    /// </summary>
    private static (long InicioMs, long FimInclusivoMs) IntervaloMes(int mes, int ano)
    {
        DateTime inicio = new DateTime(ano, mes, 1, 0, 0, 0, DateTimeKind.Local);
        int ultimoDia = DateTime.DaysInMonth(ano, mes);
        DateTime fim = new DateTime(ano, mes, ultimoDia, 23, 59, 59, DateTimeKind.Local);
        long inicioMs = new DateTimeOffset(inicio).ToUnixTimeMilliseconds();
        long fimInclusivoMs = new DateTimeOffset(fim).ToUnixTimeMilliseconds();
        return (inicioMs, fimInclusivoMs);
    }
}
