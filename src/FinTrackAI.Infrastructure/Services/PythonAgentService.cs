using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using FinTrackAI.Domain.Interfaces.Services;
using Microsoft.Extensions.Configuration;

namespace FinTrackAI.Infrastructure.Services;

public sealed class PythonAgentService : IPythonAgentService
{
    private readonly HttpClient _http;
    private readonly string _pythonUrl;

    public PythonAgentService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _pythonUrl = config["PythonAgents:Url"]?.TrimEnd('/')
                     ?? "http://localhost:8001";
    }

    public async IAsyncEnumerable<string> ChatAsync(
        string mensagem,
        IReadOnlyList<ChatHistoricoItem> historico,
        string usuarioId,
        int mes,
        int ano,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        object body = new
        {
            mensagem,
            historico = historico.Select(h => new { role = h.Role, content = h.Content }),
            usuario_id = usuarioId,
            mes,
            ano,
        };

        using HttpRequestMessage request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_pythonUrl}/agentes/chat")
        {
            Content = JsonContent.Create(body),
        };

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        using HttpResponseMessage response = await _http.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        response.EnsureSuccessStatusCode();

        using Stream stream = await response.Content.ReadAsStreamAsync(cts.Token);
        using StreamReader reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cts.Token.IsCancellationRequested)
        {
            string? linha = await reader.ReadLineAsync(cts.Token);
            if (linha == null)
            {
                break;
            }

            if (!linha.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            string dados = linha[6..].Trim();
            if (dados == "[DONE]")
            {
                yield break;
            }

            if (string.IsNullOrEmpty(dados))
            {
                continue;
            }

            yield return dados;
        }
    }
}
