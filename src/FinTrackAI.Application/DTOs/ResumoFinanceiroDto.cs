namespace FinTrackAI.Application.DTOs;

public sealed class ResumoFinanceiroDto
{
    public int Mes { get; set; }

    public int Ano { get; set; }

    public double TotalReceitas { get; set; }

    public double TotalDespesas { get; set; }

    public double Saldo { get; set; }
}
