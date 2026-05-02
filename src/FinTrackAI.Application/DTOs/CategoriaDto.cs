namespace FinTrackAI.Application.DTOs;

public sealed class CategoriaDto
{
    public int Id { get; set; }

    public string? Nome { get; set; }

    public int? TipoMovimento { get; set; }

    public string? Cor { get; set; }
}
