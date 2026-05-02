namespace FinTrackAI.Domain.Entities;

/// <summary>Cadastro "Minha Renda" (<c>fontes_renda</c>).</summary>
public sealed class FonteRenda
{
    public int Id { get; set; }

    public string? Nome { get; set; }

    public float ValorBase { get; set; }

    public int? Fixa { get; set; }

    public int DiaPrevisto { get; set; }

    public int? Ativa { get; set; }

    public int? IncluirNaRendaDiaria { get; set; }
}
