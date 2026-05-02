namespace FinTrackAI.Infrastructure.Services;

public sealed class ImportacaoState
{
    private readonly object _sync = new object();

    public DateTimeOffset? UltimaImportacaoUtc { get; private set; }

    public void RegistrarImportacaoConcluida()
    {
        lock (_sync)
        {
            UltimaImportacaoUtc = DateTimeOffset.UtcNow;
        }
    }
}
