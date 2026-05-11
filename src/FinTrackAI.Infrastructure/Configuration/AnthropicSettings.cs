namespace FinTrackAI.Infrastructure.Configuration;

public sealed class AnthropicSettings
{
    public const string SectionName = "Anthropic";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Modelo padrão quando ModelSimples/ModelMedio/ModelComplexo não forem definidos.</summary>
    public string Model { get; set; } = "claude-haiku-4-5-20251001";

    public int MaxTokens { get; set; } = 1024;

    /// <summary>Haiku para classificação de complexidade (barato).</summary>
    public string ModelClassifier { get; set; } = "claude-haiku-4-5-20251001";

    public string? ModelSimples { get; set; }

    public string? ModelMedio { get; set; }

    public string? ModelComplexo { get; set; }

    public int MaxTokensSimples { get; set; }

    public int MaxTokensMedio { get; set; }

    public int MaxTokensComplexo { get; set; }
}
