using System.Globalization;

namespace FinTrackAI.Domain.Entities;

public sealed class Lancamento
{
    public int Id { get; set; }

    public float Valor { get; set; }

    public string? Descricao { get; set; }

    public int? FormaPagamento { get; set; }

    public long DataHora { get; set; }

    public int PagamentoFatura { get; set; }

    public int Pago { get; set; }

    public long? DataPagamento { get; set; }

    public int Categoria { get; set; }

    public string? GrupoParcelas { get; set; }

    public int? ParcelaNumero { get; set; }

    public int? ParcelaTotal { get; set; }

    public int? IdCartao { get; set; }

    public int? IdConta { get; set; }

    public int? TipoMovimento { get; set; }

    public int? IdCategoriaPersonalizada { get; set; }

    public int? TipoDespesa { get; set; }

    public int? IdSubcategoriaPersonalizada { get; set; }

    public string DataHoraFormatada =>
        DateTimeOffset.FromUnixTimeMilliseconds(DataHora).LocalDateTime.ToString(
            "dd/MM/yyyy HH:mm",
            CultureInfo.GetCultureInfo("pt-BR"));
}
