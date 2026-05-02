using FinTrackAI.Application.DTOs;
using FinTrackAI.Domain.Interfaces.Services;

namespace FinTrackAI.Application.UseCases.Chat;

public sealed class EnviarMensagemUseCase
{
    private readonly IAgentService _agentService;

    public EnviarMensagemUseCase(IAgentService agentService)
    {
        _agentService = agentService;
    }

    public IAsyncEnumerable<string> Execute(ChatRequestDto request, CancellationToken cancellationToken)
    {
        request.Historico ??= new List<ChatHistoricoDto>();
        List<ChatHistoricoItem> historico = new List<ChatHistoricoItem>();
        foreach (ChatHistoricoDto item in request.Historico)
        {
            historico.Add(new ChatHistoricoItem(item.ResolveRole(), item.ResolveContent()));
        }

        ChatAgentRequest agentRequest = new ChatAgentRequest(request.Mensagem, historico);
        return _agentService.ProcessChatAsync(agentRequest, cancellationToken);
    }
}
