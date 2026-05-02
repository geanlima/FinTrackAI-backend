namespace FinTrackAI.Application.DTOs;

public sealed class ContaPagarDto
{
    public int Id { get; set; }

    public string? Descricao { get; set; }

    public float Valor { get; set; }

    public long DataVencimento { get; set; }

    public int FormaPagamento { get; set; }

    public int IdCartao { get; set; }

    public int IdConta { get; set; }
}
