using FinTrackAI.Application.DTOs;
using FinTrackAI.Application.UseCases.Lancamentos;
using Microsoft.AspNetCore.Mvc;

namespace FinTrackAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class LancamentosController : ControllerBase
{
    private readonly GetLancamentosByPeriodoUseCase _porPeriodo;
    private readonly GetResumoMensalUseCase _resumo;
    private readonly GetGastosPorCategoriaUseCase _gastosCategoria;
    private readonly ILogger<LancamentosController> _logger;

    public LancamentosController(
        GetLancamentosByPeriodoUseCase porPeriodo,
        GetResumoMensalUseCase resumo,
        GetGastosPorCategoriaUseCase gastosCategoria,
        ILogger<LancamentosController> logger)
    {
        _porPeriodo = porPeriodo;
        _resumo = resumo;
        _gastosCategoria = gastosCategoria;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LancamentoDto>>> GetPorMes(
        [FromQuery] int mes,
        [FromQuery] int ano,
        CancellationToken cancellationToken)
    {
        if (mes is < 1 or > 12 || ano is < 1900 or > 2100)
        {
            return BadRequest("Informe mes (1–12) e ano válidos na query string.");
        }

        IReadOnlyList<LancamentoDto> lista = await _porPeriodo.ExecuteAsync(mes, ano, cancellationToken);
        return Ok(lista);
    }

    [HttpGet("resumo")]
    public async Task<ActionResult<ResumoFinanceiroDto>> GetResumo(
        [FromQuery] int mes,
        [FromQuery] int ano,
        CancellationToken cancellationToken)
    {
        if (mes is < 1 or > 12 || ano is < 1900 or > 2100)
        {
            return BadRequest("Informe mes (1–12) e ano válidos na query string.");
        }

        try
        {
            ResumoFinanceiroDto dto = await _resumo.ExecuteAsync(mes, ano, cancellationToken);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao obter resumo mensal {Mes}/{Ano}", mes, ano);
            return StatusCode(
                500,
                new { mensagem = "Não foi possível calcular o resumo do período.", detalhe = ex.Message });
        }
    }

    [HttpGet("gastos-por-categoria")]
    public async Task<ActionResult<IReadOnlyList<GastoCategoriaDto>>> GetGastosCategoria(
        [FromQuery] int mes,
        [FromQuery] int ano,
        CancellationToken cancellationToken)
    {
        if (mes is < 1 or > 12 || ano is < 1900 or > 2100)
        {
            return BadRequest("Informe mes (1–12) e ano válidos na query string.");
        }

        try
        {
            IReadOnlyList<GastoCategoriaDto> lista =
                await _gastosCategoria.ExecuteAsync(mes, ano, cancellationToken);
            return Ok(lista);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao obter gastos por categoria {Mes}/{Ano}", mes, ano);
            return StatusCode(
                500,
                new { mensagem = "Não foi possível listar gastos por categoria.", detalhe = ex.Message });
        }
    }
}
