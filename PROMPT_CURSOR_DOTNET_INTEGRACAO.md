# PROMPT CURSOR — FinTrack AI .NET — Integração com Agentes Python

## CONTEXTO

O FinTrack AI já tem uma API .NET Web API com Clean Architecture funcionando.
Preciso adicionar a integração com o microsserviço Python de agentes
que roda em `http://localhost:8001`.

O Angular já chama `POST /api/Chat` com streaming SSE.
O .NET precisa agora delegar essa chamada para o Python internamente.

---

## O QUE JÁ EXISTE (não mexer)

- `ChatController` com endpoint `POST /api/Chat` que faz streaming SSE
- `IAgentService` e `AgentService` com o agente simples atual
- Toda a estrutura Clean Architecture

---

## O QUE PRECISA SER ALTERADO

### 1. Novo serviço de proxy para o Python

Criar em `FinTrackAI.Infrastructure/Services/`:

```csharp
// IPythonAgentService.cs no Domain
public interface IPythonAgentService
{
    IAsyncEnumerable<string> ChatAsync(
        string mensagem,
        List<ChatMessage> historico,
        string usuarioId,
        int mes,
        int ano,
        CancellationToken ct = default);
}

// PythonAgentService.cs na Infrastructure
public class PythonAgentService : IPythonAgentService
{
    private readonly HttpClient _http;
    private readonly string _pythonUrl;

    public PythonAgentService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _pythonUrl = config["PythonAgents:Url"]
                     ?? "http://localhost:8001";
    }

    public async IAsyncEnumerable<string> ChatAsync(
        string mensagem,
        List<ChatMessage> historico,
        string usuarioId,
        int mes,
        int ano,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var body = new
        {
            mensagem,
            historico = historico.Select(h => new
            {
                role    = h.Role,
                content = h.Content
            }),
            usuario_id = usuarioId,
            mes,
            ano
        };

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{_pythonUrl}/agentes/chat")
        {
            Content = JsonContent.Create(body)
        };

        // Timeout de 5 minutos para respostas longas
        using var cts = CancellationTokenSource
            .CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromMinutes(5));

        var response = await _http.SendAsync(request,
            HttpCompletionOption.ResponseHeadersRead, cts.Token);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content
            .ReadAsStreamAsync(cts.Token);
        using var reader = new StreamReader(stream);

        // Lê linha a linha do SSE do Python
        while (!reader.EndOfStream && !cts.Token.IsCancellationRequested)
        {
            var linha = await reader.ReadLineAsync(cts.Token);
            if (linha == null) break;
            if (!linha.StartsWith("data: ")) continue;

            var dados = linha[6..].Trim();
            if (dados == "[DONE]") yield break;
            if (string.IsNullOrEmpty(dados)) continue;

            yield return dados;
        }
    }
}
```

### 2. Registrar no Program.cs

```csharp
// Adicionar após os outros services:
builder.Services.AddHttpClient<IPythonAgentService, PythonAgentService>();
```

### 3. Adicionar configuração no appsettings.json

```json
{
  "PythonAgents": {
    "Url": "http://localhost:8001"
  }
}
```

### 4. Atualizar o ChatController

```csharp
// Injetar IPythonAgentService no controller
// No endpoint POST /api/Chat:
// 1. Tentar chamar o Python primeiro
// 2. Se Python não estiver disponível (HttpRequestException),
//    fazer fallback para o agente .NET existente
// 3. Fazer proxy do SSE do Python para o Angular

[HttpPost]
public async Task Chat([FromBody] ChatRequest request,
    CancellationToken ct)
{
    Response.Headers["Content-Type"]  = "text/event-stream";
    Response.Headers["Cache-Control"] = "no-cache";
    Response.Headers["X-Accel-Buffering"] = "no";

    var mes = request.Mes ?? DateTime.Now.Month;
    var ano = request.Ano ?? DateTime.Now.Year;

    try
    {
        // Tenta usar os agentes Python
        await foreach (var evento in _pythonAgentService.ChatAsync(
            request.Mensagem,
            request.Historico ?? [],
            request.UsuarioId ?? "default",
            mes, ano, ct))
        {
            await Response.WriteAsync($"data: {evento}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }
    catch (HttpRequestException)
    {
        // Fallback: usa o agente .NET existente
        // (mantém o comportamento anterior)
        await foreach (var token in _agentService.ChatAsync(
            request.Mensagem, request.Historico ?? [], ct))
        {
            await Response.WriteAsync(
                $"data: {{\"tipo\":\"token\",\"conteudo\":\"{token}\"}}\n\n",
                ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    await Response.WriteAsync("data: [DONE]\n\n", ct);
    await Response.Body.FlushAsync(ct);
}
```

### 5. Atualizar o ChatRequest DTO

```csharp
// Adicionar campos novos ao DTO existente:
public record ChatRequest(
    string Mensagem,
    List<ChatMessage>? Historico = null,
    string? UsuarioId = null,
    int? Mes = null,      // ← novo
    int? Ano = null       // ← novo
);
```

---

## ANGULAR — NENHUMA MUDANÇA NECESSÁRIA

O Angular continua chamando `POST /api/Chat` normalmente.
O streaming SSE funciona igual — o .NET faz proxy transparente do Python.

---

## DOCKER COMPOSE — ADICIONAR O SERVIÇO PYTHON

No `docker-compose.yml` existente, adicionar:

```yaml
  agentes-python:
    build:
      context: ../vox-finance-ia-agentes   # pasta do projeto Python
      dockerfile: Dockerfile
    container_name: vox_agentes_python
    environment:
      - ANTHROPIC_API_KEY=${ANTHROPIC_API_KEY}
      - SERPER_API_KEY=${SERPER_API_KEY}
      - POSTGRES_URL=postgresql://postgres:postgres@postgres:5432/fintrack
    ports:
      - "8001:8001"
    depends_on:
      postgres:
        condition: service_healthy
    restart: unless-stopped
```

E no serviço `backend`, atualizar a configuração:
```yaml
environment:
  - PythonAgents__Url=http://agentes-python:8001
```

---

## RESULTADO ESPERADO

Após as alterações:

1. `POST /api/Chat` → .NET recebe → chama Python em `localhost:8001`
2. Python processa com LangGraph e retorna SSE
3. .NET faz proxy do SSE para o Angular
4. Se Python estiver offline → fallback automático para agente .NET

O Angular continua funcionando exatamente igual — sem nenhuma mudança.


