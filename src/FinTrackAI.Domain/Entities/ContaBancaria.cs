namespace FinTrackAI.Domain.Entities;

public sealed class ContaBancaria
{
    public int Id { get; set; }

    public string? Descricao { get; set; }

    public string? Banco { get; set; }

    public string? Agencia { get; set; }

    public string? Numero { get; set; }

    public string? Tipo { get; set; }

    public int Ativa { get; set; }
}
