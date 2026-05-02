using System.Text.Json.Serialization;

namespace FinTrackAI.Application.DTOs;

public sealed class ChatRequestDto
{
    [JsonPropertyName("mensagem")]
    public string Mensagem { get; set; } = string.Empty;

    [JsonPropertyName("historico")]
    public List<ChatHistoricoDto> Historico { get; set; } = new();
}

public sealed class ChatHistoricoDto
{
    [JsonPropertyName("papel")]
    public string? Papel { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("conteudo")]
    public string? Conteudo { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    public string ResolveRole() => !string.IsNullOrWhiteSpace(Role) ? Role : (Papel ?? "user");

    public string ResolveContent() => !string.IsNullOrWhiteSpace(Content) ? Content : (Conteudo ?? string.Empty);
}
