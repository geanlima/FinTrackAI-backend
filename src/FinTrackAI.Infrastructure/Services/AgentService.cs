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

        List<Anthropic.SDK.Common.Tool> tools = CriarFerramentas(cancellationToken);
        List<Message> messages = new List<Message>();
        foreach (ChatHistoricoItem item in request.Historico)
        {
            RoleType role = item.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? RoleType.Assistant
                : RoleType.User;
            messages.Add(new Message(role, item.Content));
        }

        messages.Add(new Message(RoleType.User, request.Mensagem));

        DateTime hoje = DateTime.Now;
        string systemPrompt = $"""
            Você é o FinTrack AI, um assistente financeiro pessoal inteligente.

            CONTEXTO TEMPORAL:
            - Hoje é {hoje:dd/MM/yyyy}
            - Mês atual: {hoje.Month}
            - Ano atual: {hoje.Year}
            - Quando o usuário disser "esse mês" ou "mês atual", use mês={hoje.Month} e ano={hoje.Year}
            - Quando disser "mês passado", use mês={hoje.AddMonths(-1).Month} e ano={hoje.AddMonths(-1).Year}

            REGRAS:
            - Sempre use as ferramentas para buscar dados reais antes de responder
            - Responda sempre em português brasileiro
            - Formate valores como R$ X.XXX,XX
            - Nunca invente dados — use apenas o que as ferramentas retornam
            - Se não encontrar dados, diga claramente
            - NÃO peça confirmação de período quando o usuário disser "esse mês" — use o mês atual diretamente

            FERRAMENTAS DISPONÍVEIS:
            - BuscarLancamentosPorPeriodo: busca transações num período
            - CalcularTotalPorCategoria: agrupa gastos por categoria
            - GetResumoMensal: resumo financeiro do mês
            - GetContasPagarPendentes: contas em aberto
            - GetGastosPorFormaPagamento: gastos por forma de pagamento
            - GetMaiorGasto: maior gasto do período
            """;

        MessageParameters parameters = new MessageParameters
        {
            Model = _settings.Model,
            MaxTokens = _settings.MaxTokens,
            Stream = false,
            Temperature = 1.0m,
            Messages = messages,
            Tools = tools,
            System = new List<SystemMessage> { new SystemMessage(systemPrompt) },
        };

        int segurancaIteracoes = 0;
        while (segurancaIteracoes < 12)
        {
            segurancaIteracoes++;
            MessageResponse? result = null;
            string? erroAnthropic = null;
            try
            {
                result = await _client.Messages.GetClaudeMessageAsync(parameters, cancellationToken);
            }
            catch (Exception ex)
            {
                erroAnthropic = ex.Message;
            }

            if (erroAnthropic != null)
            {
                yield return
                    "Erro ao chamar a API Anthropic: "
                    + erroAnthropic
                    + ". Confira ANTHROPIC_API_KEY, conexão e o modelo configurado (Anthropic:Model).";
                yield break;
            }

            messages.Add(result!.Message);
            parameters.Messages = messages;

            if (result.ToolCalls != null && result.ToolCalls.Count > 0)
            {
                foreach (dynamic toolCall in result.ToolCalls)
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

                parameters.Messages = messages;
                continue;
            }

            string textoFinal = ExtrairTextoAssistant(result.Message);
            foreach (string chunk in DividirTexto(textoFinal, 120))
            {
                yield return chunk;
            }

            yield break;
        }

        yield return "Não foi possível concluir a resposta do assistente (limite de iterações com ferramentas).";
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

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
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

    private static IEnumerable<string> DividirTexto(string texto, int tamanhoMaximo)
    {
        if (string.IsNullOrEmpty(texto))
        {
            yield return string.Empty;
            yield break;
        }

        int i = 0;
        while (i < texto.Length)
        {
            int len = Math.Min(tamanhoMaximo, texto.Length - i);
            yield return texto.Substring(i, len);
            i += len;
        }
    }
}
