namespace FinTrackAI.Domain.Entities;

public sealed class ContaPagar
{
    public int Id { get; set; }

    public string? Descricao { get; set; }

    public float Valor { get; set; }

    public long DataVencimento { get; set; }

    public int Pago { get; set; }

    public long DataPagamento { get; set; }

    public int ParcelaNumero { get; set; }

    public int ParcelaTotal { get; set; }

    public string? GrupoParcelas { get; set; }

    public int FormaPagamento { get; set; }

    public int IdCartao { get; set; }

    public int IdConta { get; set; }

    public int IdLancamento { get; set; }

    public long DataCabecalho { get; set; }
}
