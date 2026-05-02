using FinTrackAI.Application.DTOs;
using FinTrackAI.Application.UseCases.ContasPagar;
using Microsoft.AspNetCore.Mvc;

namespace FinTrackAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ContasPagarController : ControllerBase
{
    private readonly GetContasPagarPendentesUseCase _pendentes;

    public ContasPagarController(GetContasPagarPendentesUseCase pendentes)
    {
        _pendentes = pendentes;
    }

    [HttpGet("pendentes")]
    public async Task<ActionResult<IReadOnlyList<ContaPagarDto>>> GetPendentes(CancellationToken cancellationToken)
    {
        IReadOnlyList<ContaPagarDto> lista = await _pendentes.ExecuteAsync(cancellationToken);
        return Ok(lista);
    }
}
