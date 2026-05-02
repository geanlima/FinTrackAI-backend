namespace FinTrackAI.Domain.Interfaces.Services;

public interface IManutencaoService
{
    Task<ConexaoStatusDto> TestarConexaoSqliteAsync(string caminho);

    Task<ConexaoStatusDto> TestarConexaoPostgresAsync(CancellationToken cancellationToken = default);

    Task<IntegracaoResultadoDto> ExecutarIntegracaoAsync(
        string caminhoSqlite,
        Func<LogEventoDto, Task> onLog,
        CancellationToken cancellationToken = default);
}

public sealed record ConexaoStatusDto(
    bool Conectado,
    string Mensagem,
    Dictionary<string, object>? Detalhes = null);

public sealed record LogEventoDto(
    string Nivel,
    string Mensagem,
    string? Tabela = null,
    int? Registros = null)
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public sealed record IntegracaoResultadoDto(
    bool Sucesso,
    int TotalRegistros,
    int TotalTabelas,
    Dictionary<string, int> RegistrosPorTabela,
    List<string> Erros,
    double TempoSegundos);
