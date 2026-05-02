namespace FinTrackAI.Application.DTOs;

public sealed class LancamentoDto
{
    public int Id { get; set; }

    public float Valor { get; set; }

    public string? Descricao { get; set; }

    public int? FormaPagamento { get; set; }

    public long DataHora { get; set; }

    public string? DataHoraFormatada { get; set; }

    public int? TipoMovimento { get; set; }

    public int? IdCategoriaPersonalizada { get; set; }

    public string? NomeCategoria { get; set; }

    public int? IdSubcategoriaPersonalizada { get; set; }

    public int? IdCartao { get; set; }

    public int? IdConta { get; set; }

    public int Pago { get; set; }
}
