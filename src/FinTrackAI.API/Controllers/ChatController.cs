using System.Net.Http;
using System.Text.Json;
using FinTrackAI.Application.DTOs;
using FinTrackAI.Application.UseCases.Chat;
using FinTrackAI.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace FinTrackAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ChatController : ControllerBase
{
    private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly EnviarMensagemUseCase _enviarMensagem;
    private readonly IPythonAgentService _pythonAgentService;

    public ChatController(EnviarMensagemUseCase enviarMensagem, IPythonAgentService pythonAgentService)
    {
        _enviarMensagem = enviarMensagem;
        _pythonAgentService = pythonAgentService;
    }

    /// <summary>
    /// Envia uma mensagem ao FinTrack AI. A resposta é transmitida em Server-Sent Events (SSE).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ChatRequestDto? body, CancellationToken cancellationToken)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.Mensagem))
        {
            return BadRequest(new { mensagem = "Informe o campo mensagem no corpo JSON." });
        }

        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        int mes = body.Mes ?? DateTime.Now.Month;
        int ano = body.Ano ?? DateTime.Now.Year;
        body.Historico ??= new List<ChatHistoricoDto>();
        List<ChatHistoricoItem> historico = new List<ChatHistoricoItem>();
        foreach (ChatHistoricoDto item in body.Historico)
        {
            historico.Add(new ChatHistoricoItem(item.ResolveRole(), item.ResolveContent()));
        }

        string usuarioId = string.IsNullOrWhiteSpace(body.UsuarioId) ? "default" : body.UsuarioId;

        try
        {
            try
            {
                await foreach (string evento in _pythonAgentService
                                   .ChatAsync(body.Mensagem, historico, usuarioId, mes, ano, cancellationToken)
                                   .WithCancellation(cancellationToken))
                {
                    await Response.WriteAsync("data: " + evento + "\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
            catch (HttpRequestException)
            {
                await foreach (string chunk in _enviarMensagem.Execute(body, cancellationToken)
                                   .WithCancellation(cancellationToken))
                {
                    string linha = "data: " + JsonSerializer.Serialize(chunk, SerializerOptions) + "\n\n";
                    await Response.WriteAsync(linha, cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }

            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            string linha = "data: "
                + JsonSerializer.Serialize(
                    "Erro ao processar o chat: " + ex.Message,
                    SerializerOptions)
                + "\n\n";
            await Response.WriteAsync(linha, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        return new EmptyResult();
    }
}
