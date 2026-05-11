namespace FinTrackAI.Domain.Interfaces.Services;

public interface IPythonAgentService
{
    IAsyncEnumerable<string> ChatAsync(
        string mensagem,
        IReadOnlyList<ChatHistoricoItem> historico,
        string usuarioId,
        int mes,
        int ano,
        CancellationToken cancellationToken = default);
}
