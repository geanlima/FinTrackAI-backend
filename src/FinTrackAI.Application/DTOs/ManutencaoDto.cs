namespace FinTrackAI.Application.DTOs;

public sealed record IntegracaoRequestDto(string? CaminhoSqlite);

/// <summary>Caminho absoluto no servidor após <c>POST upload-sqlite</c>.</summary>
public sealed record UploadSqliteResponseDto(string CaminhoSqlite);

public sealed record ManutencaoStatusDto(
    int TotalLancamentos,
    long? DataMaisRecenteLancamentoMs,
    string? DataMaisRecenteLancamentoFormatada,
    Dictionary<string, int> TotalPorTabela,
    DateTimeOffset? UltimaImportacaoUtc);

public sealed record AnthropicApiKeyBodyDto(string? ApiKey);

/// <param name="Origem"><c>memoria</c>, <c>ambiente</c> ou <c>nenhuma</c>.</param>
public sealed record AnthropicKeyStatusDto(bool Configurada, string Origem);
