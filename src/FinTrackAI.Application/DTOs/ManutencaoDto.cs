namespace FinTrackAI.Application.DTOs;

public sealed record IntegracaoRequestDto(string? CaminhoSqlite);

/// <summary>Define a fonte de leitura da API e do agente. <c>caminhoSqlite</c> vazio ou ausente volta ao PostgreSQL.</summary>
public sealed record FonteDadosRequestDto(string? CaminhoSqlite);

/// <summary>Caminho absoluto no servidor após <c>POST upload-sqlite</c>.</summary>
public sealed record UploadSqliteResponseDto(string CaminhoSqlite);

/// <param name="FonteDados"><c>postgres</c> ou <c>sqlite</c>.</param>
public sealed record ManutencaoStatusDto(
    int TotalLancamentos,
    long? DataMaisRecenteLancamentoMs,
    string? DataMaisRecenteLancamentoFormatada,
    Dictionary<string, int> TotalPorTabela,
    DateTimeOffset? UltimaImportacaoUtc,
    string FonteDados = "postgres",
    string? SqliteArquivo = null);

/// <param name="Modo"><c>postgres</c> ou <c>sqlite</c>.</param>
public sealed record FonteDadosAtualDto(string Modo, string? SqliteArquivo);

public sealed record AnthropicApiKeyBodyDto(string? ApiKey);

/// <param name="Origem"><c>memoria</c>, <c>ambiente</c> ou <c>nenhuma</c>.</param>
public sealed record AnthropicKeyStatusDto(bool Configurada, string Origem);
