namespace FinTrackAI.Domain.Entities;

public sealed class Subcategoria
{
    public int Id { get; set; }

    public int IdCategoriaPersonalizada { get; set; }

    public string? Nome { get; set; }

    public long CriadoEm { get; set; }
}
