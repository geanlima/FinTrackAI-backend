using FinTrackAI.Application.DTOs;
using FinTrackAI.Application.UseCases.Categorias;
using Microsoft.AspNetCore.Mvc;

namespace FinTrackAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoriasController : ControllerBase
{
    private readonly GetCategoriasUseCase _useCase;

    public CategoriasController(GetCategoriasUseCase useCase)
    {
        _useCase = useCase;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoriaDto>>> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyList<CategoriaDto> lista = await _useCase.ExecuteAsync(cancellationToken);
        return Ok(lista);
    }
}
