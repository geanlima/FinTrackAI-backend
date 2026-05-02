namespace FinTrackAI.Infrastructure.Services;

/// <summary>
/// Chave Anthropic informada pela tela de manutenção (memória do processo; reinício apaga).
/// Tem precedência sobre appsettings / variáveis de ambiente.
/// </summary>
public sealed class AnthropicApiKeyState
{
    private readonly object _lock = new();
    private string? _override;

    public string? OverrideKey
    {
        get
        {
            lock (_lock)
            {
                return _override;
            }
        }
    }

    public bool HasOverride
    {
        get
        {
            lock (_lock)
            {
                return !string.IsNullOrWhiteSpace(_override);
            }
        }
    }

    public void SetOverride(string? apiKey)
    {
        lock (_lock)
        {
            _override = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim();
        }
    }
}
