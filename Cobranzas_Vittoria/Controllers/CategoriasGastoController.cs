using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

[ApiController]
[Route("api/maestra/categorias-gasto")]
public class CategoriasGastoController : ControllerBase
{
    private readonly ICategoriaGastoService _service;
    public CategoriasGastoController(ICategoriaGastoService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? activo)
        => Ok(await _service.ListAsync(activo));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoriaGastoUpsertDto dto)
        => Ok(new { IdCategoriaGasto = await _service.UpsertAsync(dto) });

    [HttpPut("{categoriaId:int}")]
    public async Task<IActionResult> Update(int categoriaId, [FromBody] CategoriaGastoUpsertDto dto)
    {
        dto.IdCategoriaGasto = categoriaId;
        return Ok(new { IdCategoriaGasto = await _service.UpsertAsync(dto) });
    }

    [HttpDelete("{categoriaId:int}")]
    public async Task<IActionResult> Delete(int categoriaId)
    {
        await _service.DeleteAsync(categoriaId);
        return Ok(new { ok = true });
    }
}
