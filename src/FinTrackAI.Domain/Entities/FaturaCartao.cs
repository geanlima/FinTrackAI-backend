namespace FinTrackAI.Domain.Entities;

public sealed class FaturaCartao
{
    public int Id { get; set; }

    public int IdCartao { get; set; }

    public int Ano { get; set; }

    public int Mes { get; set; }

    public long DataFechamento { get; set; }

    public long DataVencimento { get; set; }

    public float ValorTotal { get; set; }

    public int Pago { get; set; }

    public long DataPagamento { get; set; }
}
