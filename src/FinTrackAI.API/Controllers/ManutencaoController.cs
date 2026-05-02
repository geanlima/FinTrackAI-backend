using System.Text.Json;
using FinTrackAI.Application.DTOs;
using FinTrackAI.Application.Helpers;
using FinTrackAI.Domain.Interfaces.Services;
using FinTrackAI.Infrastructure.Configuration;
using FinTrackAI.Infrastructure.Data;
using FinTrackAI.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinTrackAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ManutencaoController : ControllerBase
{
    private const long TamanhoMaximoUploadBytes = 52_428_800;

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IManutencaoService _manutencao;
    private readonly FinTrackDbContext _db;
    private readonly ImportacaoState _importacaoState;
    private readonly DataSourceState _dataSource;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly AnthropicApiKeyState _anthropicKeyState;
    private readonly IOptions<AnthropicSettings> _anthropicSettings;

    public ManutencaoController(
        IManutencaoService manutencao,
        FinTrackDbContext db,
        ImportacaoState importacaoState,
        DataSourceState dataSource,
        IConfiguration configuration,
        IWebHostEnvironment env,
        AnthropicApiKeyState anthropicKeyState,
        IOptions<AnthropicSettings> anthropicSettings)
    {
        _manutencao = manutencao;
        _db = db;
        _importacaoState = importacaoState;
        _dataSource = dataSource;
        _configuration = configuration;
        _env = env;
        _anthropicKeyState = anthropicKeyState;
        _anthropicSettings = anthropicSettings;
    }

    /// <summary>Indica se a API está lendo PostgreSQL ou o SQLite definido em <c>POST fonte-dados</c>.</summary>
    [HttpGet("fonte-dados")]
    public ActionResult<FonteDadosAtualDto> GetFonteDados()
    {
        bool sqlite = _dataSource.UsandoSqlite;
        return Ok(new FonteDadosAtualDto(
            sqlite ? "sqlite" : "postgres",
            sqlite ? Path.GetFileName(_dataSource.CaminhoSqliteAtivo!) : null));
    }

    /// <summary>
    /// Define o arquivo SQLite usado por toda a API (lançamentos, categorias, chat/agente).
    /// Caminho absoluto no servidor (ex.: retorno de <c>upload-sqlite</c>). Corpo vazio ou <c>caminhoSqlite</c> vazio volta ao PostgreSQL.
    /// </summary>
    [HttpPost("fonte-dados")]
    public async Task<ActionResult<object>> DefinirFonteDados([FromBody] FonteDadosRequestDto? body)
    {
        if (body == null)
        {
            return BadRequest(new { mensagem = "Informe o corpo JSON (use caminhoSqlite vazio para PostgreSQL)." });
        }

        if (string.IsNullOrWhiteSpace(body.CaminhoSqlite))
        {
            _dataSource.DefinirPostgres();
            return Ok(new
            {
                mensagem = "Fonte de dados: PostgreSQL (ConnectionStrings:DefaultConnection).",
                modo = "postgres",
            });
        }

        ConexaoStatusDto teste = await _manutencao.TestarConexaoSqliteAsync(body.CaminhoSqlite);
        if (!teste.Conectado)
        {
            return BadRequest(teste);
        }

        _dataSource.DefinirSqlite(body.CaminhoSqlite);
        return Ok(new
        {
            mensagem =
                "Fonte de dados: SQLite. Consultas e agente usam este arquivo até você voltar ao PostgreSQL.",
            modo = "sqlite",
            arquivo = Path.GetFileName(_dataSource.CaminhoSqliteAtivo!),
            caminhoSqlite = _dataSource.CaminhoSqliteAtivo,
        });
    }

    [HttpGet("anthropic-api-key/status")]
    [HttpGet("chave-anthropic/status")]
    public ActionResult<AnthropicKeyStatusDto> GetAnthropicKeyStatus()
    {
        bool memoria = _anthropicKeyState.HasOverride;
        bool ambiente = !string.IsNullOrWhiteSpace(_anthropicSettings.Value.ApiKey);
        bool configurada = memoria || ambiente;
        string origem = memoria ? "memoria" : ambiente ? "ambiente" : "nenhuma";
        return Ok(new AnthropicKeyStatusDto(configurada, origem));
    }

    /// <summary>Grava a chave Anthropic em memória (válida até reiniciar a API). Body JSON: <c>{"apiKey":"sk-ant-..."}</c>. Envie <c>null</c> ou string vazia para limpar a chave da memória.</summary>
    [HttpPost("anthropic-api-key")]
    [HttpPost("chave-anthropic")]
    public ActionResult<object> DefinirAnthropicApiKey([FromBody] AnthropicApiKeyBodyDto? body)
    {
        _anthropicKeyState.SetOverride(body?.ApiKey);
        string mensagem = string.IsNullOrWhiteSpace(body?.ApiKey)
            ? "Chave removida da memória. A API volta a usar apenas variáveis de ambiente / appsettings (se houver)."
            : "Chave salva na memória deste processo. Vale até reiniciar o servidor ou o container.";
        return Ok(new { mensagem });
    }

    [HttpGet("testar-sqlite")]
    public async Task<ActionResult<ConexaoStatusDto>> TestarSqlite([FromQuery] string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
        {
            return BadRequest(new ConexaoStatusDto(false, "Informe o parâmetro caminho."));
        }

        ConexaoStatusDto resultado = await _manutencao.TestarConexaoSqliteAsync(caminho);
        return resultado.Conectado ? Ok(resultado) : BadRequest(resultado);
    }

    [HttpGet("testar-postgres")]
    public async Task<ActionResult<ConexaoStatusDto>> TestarPostgres(CancellationToken cancellationToken)
    {
        ConexaoStatusDto resultado = await _manutencao.TestarConexaoPostgresAsync(cancellationToken);
        return resultado.Conectado ? Ok(resultado) : StatusCode(503, resultado);
    }

    [HttpGet("status")]
    public async Task<ActionResult<ManutencaoStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        int totalLancamentos = await _db.Lancamentos.CountAsync(cancellationToken);
        long? dataMaisRecenteMs = null;
        if (totalLancamentos > 0)
        {
            dataMaisRecenteMs = await _db.Lancamentos.MaxAsync(l => (long?)l.DataHora, cancellationToken);
        }

        string? dataFormatada = null;
        if (dataMaisRecenteMs.HasValue)
        {
            dataFormatada = DateTimeHelper.FromTimestampMs(dataMaisRecenteMs.Value).ToString("dd/MM/yyyy HH:mm");
        }

        Dictionary<string, int> totais = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["lancamentos"] = await _db.Lancamentos.CountAsync(cancellationToken),
            ["categorias_personalizadas"] = await _db.CategoriasPersonalizadas.CountAsync(cancellationToken),
            ["subcategorias_personalizadas"] = await _db.SubcategoriasPersonalizadas.CountAsync(cancellationToken),
            ["conta_bancaria"] = await _db.ContasBancarias.CountAsync(cancellationToken),
            ["cartao_credito"] = await _db.CartoesCredito.CountAsync(cancellationToken),
            ["conta_pagar"] = await _db.ContasPagar.CountAsync(cancellationToken),
            ["despesas_fixas"] = await _db.DespesasFixas.CountAsync(cancellationToken),
            ["fatura_cartao"] = await _db.FaturasCartao.CountAsync(cancellationToken),
        };

        bool sqlite = _dataSource.UsandoSqlite;
        ManutencaoStatusDto status = new ManutencaoStatusDto(
            totalLancamentos,
            dataMaisRecenteMs,
            dataFormatada,
            totais,
            _importacaoState.UltimaImportacaoUtc,
            sqlite ? "sqlite" : "postgres",
            sqlite ? Path.GetFileName(_dataSource.CaminhoSqliteAtivo!) : null);

        return Ok(status);
    }

    /// <summary>Recebe o arquivo SQLite do navegador, grava em disco e devolve o caminho usado em testar/integrar.</summary>
    [HttpPost("upload-sqlite")]
    [RequestSizeLimit(TamanhoMaximoUploadBytes)]
    public async Task<ActionResult<UploadSqliteResponseDto>> UploadSqlite(
        IFormFile? file,
        [FromQuery] bool usarComoFonteDados = false,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { mensagem = "Envie um arquivo no campo file (multipart/form-data)." });
        }

        if (file.Length > TamanhoMaximoUploadBytes)
        {
            return BadRequest(new { mensagem = "Arquivo excede o limite de 50 MB." });
        }

        string ext = Path.GetExtension(file.FileName);
        if (!ext.Equals(".db", StringComparison.OrdinalIgnoreCase)
            && !ext.Equals(".sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { mensagem = "Extensão inválida. Use .db ou .sqlite." });
        }

        string? pastaConfig = _configuration["Manutencao:PastaUploadSqlite"]?.Trim();
        string pasta = string.IsNullOrEmpty(pastaConfig)
            ? Path.Combine(_env.ContentRootPath, "App_Data", "sqlite_uploads")
            : Path.GetFullPath(pastaConfig);

        try
        {
            Directory.CreateDirectory(pasta);
        }
        catch (Exception ex)
        {
            return StatusCode(
                500,
                new { mensagem = $"Não foi possível preparar a pasta de upload: {ex.Message}" });
        }

        string nomeArquivo = $"{Guid.NewGuid():N}{ext}";
        string caminhoCompleto = Path.GetFullPath(Path.Combine(pasta, nomeArquivo));

        try
        {
            await using FileStream fs = new FileStream(
                caminhoCompleto,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            await file.CopyToAsync(fs, cancellationToken);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { mensagem = $"Falha ao salvar arquivo: {ex.Message}" });
        }

        ConexaoStatusDto teste = await _manutencao.TestarConexaoSqliteAsync(caminhoCompleto);
        if (!teste.Conectado)
        {
            try
            {
                System.IO.File.Delete(caminhoCompleto);
            }
            catch
            {
                // ignorar falha ao remover arquivo inválido
            }

            return BadRequest(new { mensagem = teste.Mensagem ?? "Arquivo não é um banco SQLite válido." });
        }

        if (usarComoFonteDados)
        {
            _dataSource.DefinirSqlite(caminhoCompleto);
        }

        return Ok(new UploadSqliteResponseDto(caminhoCompleto));
    }

    [HttpPost("integrar")]
    public async Task Integrar([FromBody] IntegracaoRequestDto request, CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";
        Response.Headers["X-Accel-Buffering"] = "no";

        if (request == null || string.IsNullOrWhiteSpace(request.CaminhoSqlite))
        {
            LogEventoDto erro = new LogEventoDto("erro", "Informe caminhoSqlite no corpo JSON.");
            await Response.WriteAsync("data: " + JsonSerializer.Serialize(erro, JsonOptions) + "\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            return;
        }

        async Task EnviarLog(LogEventoDto log)
        {
            await Response.WriteAsync("data: " + JsonSerializer.Serialize(log, JsonOptions) + "\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        IntegracaoResultadoDto resultado =
            await _manutencao.ExecutarIntegracaoAsync(request.CaminhoSqlite, EnviarLog, cancellationToken);

        string resultadoJson = JsonSerializer.Serialize(resultado, JsonOptions);
        await Response.WriteAsync(
            "data: " + "{\"tipo\":\"resultado\",\"dados\":" + resultadoJson + "}\n\n",
            cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
