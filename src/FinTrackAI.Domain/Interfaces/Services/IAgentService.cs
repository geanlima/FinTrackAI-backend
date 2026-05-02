namespace FinTrackAI.Domain.Interfaces.Services;

public sealed record ChatHistoricoItem(string Role, string Content);

public sealed record ChatAgentRequest(string Mensagem, IReadOnlyList<ChatHistoricoItem> Historico);

public interface IAgentService
{
    IAsyncEnumerable<string> ProcessChatAsync(ChatAgentRequest request, CancellationToken cancellationToken = default);
}
