using System.Text;
using System.Text.Json;
using Anthropic.SDK;
using Anthropic.SDK.Common;
using Anthropic.SDK.Messaging;
using FinTrackAI.Application.Services;
using FinTrackAI.Domain.Interfaces.Services;
using FinTrackAI.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace FinTrackAI.Infrastructure.Services;

public sealed class AgentService : IAgentService
{
    private const int HistoricoMaxMensagens = 6;

    private readonly AnthropicClient _client;
    private readonly FinanceiroQueryService _financeiro;
    private readonly AnthropicSettings _settings;
    private readonly AnthropicApiKeyState _apiKeyState;

    public AgentService(
        AnthropicClient client,
        FinanceiroQueryService financeiro,
        IOptions<AnthropicSettings> settings,
        AnthropicApiKeyState apiKeyState)
    {
        _client = client;
        _financeiro = financeiro;
        _settings = settings.Value;
        _apiKeyState = apiKeyState;
    }

    public async IAsyncEnumerable<string> ProcessChatAsync(
        ChatAgentRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        string chaveEfetiva = _apiKeyState.OverrideKey ?? _settings.ApiKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(chaveEfetiva))
        {
            yield return "Configure a chave da API Anthropic: use a seção **Chave Anthropic** em Manutenção, ou Anthropic:ApiKey / ANTHROPIC_API_KEY no ambiente.";
            yield break;
        }

        string complexidade = await ClassificarComplexidadeAsync(request.Mensagem, cancellationToken);
        string modelo = SelecionarModelo(complexidade);
        int maxTokens = MaxTokensPara(complexidade);

        DateTime hoje = DateTime.Now;
        string systemPrompt = MontarSystemPrompt(hoje);

        List<Anthropic.SDK.Common.Tool> tools = CriarFerramentas(cancellationToken);
        List<Message> messages = new List<Message>();
        IReadOnlyList<ChatHistoricoItem> historicoLimitado = LimitarHistorico(request.Historico, HistoricoMaxMensagens);
        foreach (ChatHistoricoItem item in historicoLimitado)
        {
            RoleType role = item.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? RoleType.Assistant
                : RoleType.User;
            messages.Add(new Message(role, item.Content));
        }

        messages.Add(new Message(RoleType.User, request.Mensagem));

        MessageParameters parameters = new MessageParameters
        {
            Model = modelo,
            MaxTokens = maxTokens,
            Stream = true,
            Temperature = 1.0m,
            Messages = messages,
            Tools = tools,
            System = new List<SystemMessage> { new SystemMessage(systemPrompt) },
            PromptCaching = PromptCacheType.AutomaticToolsAndSystem,
        };

        int segurancaIteracoes = 0;
        while (segurancaIteracoes < 12)
        {
            segurancaIteracoes++;
            parameters.Messages = messages;

            List<MessageResponse> streamOutputs = new List<MessageResponse>();
            await foreach (
                MessageResponse res in _client.Messages.StreamClaudeMessageAsync(parameters)
                    .WithCancellation(cancellationToken))
            {
                if (res.Delta?.Text is { Length: > 0 } deltaText)
                {
                    yield return deltaText;
                }

                streamOutputs.Add(res);
            }

            if (streamOutputs.Count == 0)
            {
                yield break;
            }

            messages.Add(new Message(streamOutputs));
            parameters.Messages = messages;

            bool temToolCalls = false;
            foreach (MessageResponse o in streamOutputs)
            {
                if (o.ToolCalls is { Count: > 0 })
                {
                    temToolCalls = true;
                    break;
                }
            }

            if (!temToolCalls)
            {
                yield break;
            }

            foreach (MessageResponse o in streamOutputs)
            {
                if (o.ToolCalls == null || o.ToolCalls.Count == 0)
                {
                    continue;
                }

                foreach (dynamic toolCall in o.ToolCalls)
                {
                    string saida;
                    try
                    {
                        saida = await toolCall.InvokeAsync<string>(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        saida = JsonSerializer.Serialize(new { erro = ex.Message });
                    }

                    messages.Add(new Message(toolCall, saida));
                }
            }

            parameters.Messages = messages;
        }

        yield return "Não foi possível concluir a resposta do assistente (limite de iterações com ferramentas).";
    }

    private static IReadOnlyList<ChatHistoricoItem> LimitarHistorico(
        IReadOnlyList<ChatHistoricoItem> historico,
        int maxMensagens)
    {
        if (historico.Count <= maxMensagens)
        {
            return historico;
        }

        return historico.Skip(historico.Count - maxMensagens).ToList();
    }

    private string ResolverModeloSimples() =>
        !string.IsNullOrWhiteSpace(_settings.ModelSimples) ? _settings.ModelSimples! : _settings.Model;

    private string ResolverModeloMedio() =>
        !string.IsNullOrWhiteSpace(_settings.ModelMedio) ? _settings.ModelMedio! : ResolverModeloSimples();

    private string ResolverModeloComplexo() =>
        !string.IsNullOrWhiteSpace(_settings.ModelComplexo) ? _settings.ModelComplexo! : ResolverModeloSimples();

    private string SelecionarModelo(string complexidade) =>
        complexidade switch
        {
            "complexo" => ResolverModeloComplexo(),
            "medio" => ResolverModeloMedio(),
            _ => ResolverModeloSimples(),
        };

    private int MaxTokensPara(string complexidade)
    {
        return complexidade switch
        {
            "complexo" when _settings.MaxTokensComplexo > 0 => _settings.MaxTokensComplexo,
            "medio" when _settings.MaxTokensMedio > 0 => _settings.MaxTokensMedio,
            _ when _settings.MaxTokensSimples > 0 => _settings.MaxTokensSimples,
            _ => _settings.MaxTokens > 0 ? _settings.MaxTokens : 512,
        };
    }

    private async Task<string> ClassificarComplexidadeAsync(string pergunta, CancellationToken cancellationToken)
    {
        string modelo = string.IsNullOrWhiteSpace(_settings.ModelClassifier)
            ? ResolverModeloSimples()
            : _settings.ModelClassifier.Trim();

        string prompt =
            """
            Classifique a complexidade desta pergunta financeira.
            Responda APENAS com uma palavra: simples, medio ou complexo

            simples: consulta direta (saldo, gasto de uma categoria, listar transações)
            medio: análise ou comparação (gastos por período, tendências, resumos)
            complexo: planejamento financeiro, projeções, decisões estratégicas, múltiplos cenários

            Pergunta:
            """
            + pergunta;

        MessageParameters p = new MessageParameters
        {
            Model = modelo,
            MaxTokens = 24,
            Stream = false,
            Temperature = 0m,
            Messages = new List<Message> { new Message(RoleType.User, prompt) },
        };

        try
        {
            MessageResponse res = await _client.Messages.GetClaudeMessageAsync(p, cancellationToken);
            string texto = ExtrairTextoAssistant(res.Message).Trim().ToLowerInvariant();
            if (texto.Contains("complexo", StringComparison.Ordinal))
            {
                return "complexo";
            }

            if (texto.Contains("medio", StringComparison.Ordinal)
                || texto.Contains("médio", StringComparison.Ordinal))
            {
                return "medio";
            }

            return "simples";
        }
        catch
        {
            return "simples";
        }
    }

    private static string MontarSystemPrompt(DateTime hoje)
    {
        int mes = hoje.Month;
        int ano = hoje.Year;

        return $"""
            Você é o FinTrack AI, assistente financeiro pessoal do app Vox Finance.

            CONTEXTO TEMPORAL:
            - Hoje: {hoje:dd/MM/yyyy}
            - Mês atual: {mes}/{ano}
            - "esse mês" = mês {mes}, ano {ano}
            - "mês passado" = mês {hoje.AddMonths(-1).Month}, ano {hoje.AddMonths(-1).Year}

            REGRAS:
            - Use as ferramentas para buscar dados reais antes de responder
            - Responda em português brasileiro
            - Formate valores como R$ X.XXX,XX
            - Nunca invente dados — use apenas o que as ferramentas retornam
            - NÃO peça confirmação de período quando o usuário disser "esse mês"
            - Para dicas de investimento, mencione que não é recomendação profissional

            FERRAMENTAS DISPONÍVEIS:
            - BuscarLancamentosPorPeriodo: busca transações num período
            - CalcularTotalPorCategoria: agrupa gastos por categoria com subcategorias
            - GetResumoMensal: resumo financeiro do mês (receitas, despesas, saldo)
            - GetContasPagarPendentes: contas em aberto com vencimento
            - GetGastosPorFormaPagamento: gastos por forma de pagamento
            - GetMaiorGasto: maior gasto do período
            """;
    }

    private List<Anthropic.SDK.Common.Tool> CriarFerramentas(CancellationToken cancellationToken)
    {
        CancellationToken ct = cancellationToken;
        FinanceiroQueryService fin = _financeiro;
        return new List<Anthropic.SDK.Common.Tool>
        {
            Anthropic.SDK.Common.Tool.FromFunc(
                "BuscarLancamentosPorPeriodo",
                async (
                    [FunctionParameter("Início do período em timestamp Unix (milissegundos).", true)]
                    long dataInicio,
                    [FunctionParameter("Fim do período em timestamp Unix (milissegundos). Use o instante exclusivo do mês seguinte para incluir o mês inteiro.", true)]
                    long dataFim) =>
                    await fin.BuscarLancamentosPorPeriodoAsync(dataInicio, dataFim, ct)),
            Anthropic.SDK.Common.Tool.FromFunc(
                "CalcularTotalPorCategoria",
                async (
                    [FunctionParameter("Mês (1-12).", true)]
                    int mes,
                    [FunctionParameter("Ano (ex.: 2026).", true)]
                    int ano) =>
                    await fin.CalcularTotalPorCategoriaAsync(mes, ano, ct)),
            Anthropic.SDK.Common.Tool.FromFunc(
                "GetResumoMensal",
                async (
                    [FunctionParameter("Mês (1-12).", true)]
                    int mes,
                    [FunctionParameter("Ano (ex.: 2026).", true)]
                    int ano) =>
                    await fin.GetResumoMensalAsync(mes, ano, ct)),
            Anthropic.SDK.Common.Tool.FromFunc(
                "GetContasPagarPendentes",
                async () => await fin.GetContasPagarPendentesAsync(ct)),
            Anthropic.SDK.Common.Tool.FromFunc(
                "GetGastosPorFormaPagamento",
                async (
                    [FunctionParameter("Mês (1-12).", true)]
                    int mes,
                    [FunctionParameter("Ano (ex.: 2026).", true)]
                    int ano) =>
                    await fin.GetGastosPorFormaPagamentoAsync(mes, ano, ct)),
            Anthropic.SDK.Common.Tool.FromFunc(
                "GetMaiorGasto",
                async (
                    [FunctionParameter("Mês (1-12).", true)]
                    int mes,
                    [FunctionParameter("Ano (ex.: 2026).", true)]
                    int ano) =>
                    await fin.GetMaiorGastoAsync(mes, ano, ct)),
        };
    }

    private static string ExtrairTextoAssistant(Message message)
    {
        if (message?.Content == null)
        {
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder();
        foreach (object bloco in message.Content)
        {
            if (bloco is TextContent tc && !string.IsNullOrEmpty(tc.Text))
            {
                sb.Append(tc.Text);
            }
        }

        if (sb.Length > 0)
        {
            return sb.ToString();
        }

        return message.ToString();
    }
}
