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

    public LancamentosController(
        GetLancamentosByPeriodoUseCase porPeriodo,
        GetResumoMensalUseCase resumo,
        GetGastosPorCategoriaUseCase gastosCategoria)
    {
        _porPeriodo = porPeriodo;
        _resumo = resumo;
        _gastosCategoria = gastosCategoria;
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

        ResumoFinanceiroDto dto = await _resumo.ExecuteAsync(mes, ano, cancellationToken);
        return Ok(dto);
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

        IReadOnlyList<GastoCategoriaDto> lista =
            await _gastosCategoria.ExecuteAsync(mes, ano, cancellationToken);
        return Ok(lista);
    }
}
