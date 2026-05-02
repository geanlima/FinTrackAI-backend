namespace FinTrackAI.Domain.Entities;

public sealed class DespesaFixa
{
    public int Id { get; set; }

    public string? Descricao { get; set; }

    public float Valor { get; set; }

    public int DiaVencimento { get; set; }

    public int FormaPagamento { get; set; }

    public int Ativo { get; set; }

    public int GerarAutomatico { get; set; }

    public long CriadoEm { get; set; }
}
