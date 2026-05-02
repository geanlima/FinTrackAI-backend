namespace FinTrackAI.Domain.Entities;

public sealed class Categoria
{
    public int Id { get; set; }

    public string? Nome { get; set; }

    public int? TipoMovimento { get; set; }

    public string? Cor { get; set; }
}
