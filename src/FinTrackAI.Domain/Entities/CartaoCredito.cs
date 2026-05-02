namespace FinTrackAI.Domain.Entities;

public sealed class CartaoCredito
{
    public int Id { get; set; }

    public string? Descricao { get; set; }

    public string? Bandeira { get; set; }

    public string? Ultimos4 { get; set; }

    public int DiaVencimento { get; set; }

    public float Limite { get; set; }

    public int DiaFechamento { get; set; }
}
