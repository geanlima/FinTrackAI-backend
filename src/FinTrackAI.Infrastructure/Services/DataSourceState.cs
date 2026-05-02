namespace FinTrackAI.Infrastructure.Services;

/// <summary>
/// Define se a API lê dados do PostgreSQL (padrão) ou de um arquivo SQLite (caminho absoluto no servidor).
/// Escopo do processo: em hospedagem compartilhada, todos os clientes compartilham a mesma fonte.
/// </summary>
public sealed class DataSourceState
{
    private readonly object _sync = new();
    private string? _caminhoSqlite;

    public string? CaminhoSqliteAtivo
    {
        get
        {
            lock (_sync)
                return _caminhoSqlite;
        }
    }

    public bool UsandoSqlite
    {
        get
        {
            lock (_sync)
                return !string.IsNullOrWhiteSpace(_caminhoSqlite);
        }
    }

    public void DefinirSqlite(string caminhoAbsoluto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoAbsoluto);
        lock (_sync)
            _caminhoSqlite = Path.GetFullPath(caminhoAbsoluto);
    }

    public void DefinirPostgres()
    {
        lock (_sync)
            _caminhoSqlite = null;
    }
}
