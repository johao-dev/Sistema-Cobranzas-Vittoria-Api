using Cobranzas_Vittoria.Dtos.Contable;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

[ApiController]
[Route("api/maestra/proveedores-terreno")]
public class ProveedoresTerrenoController : ControllerBase
{
    private readonly IProveedorTerrenoService _service;
    public ProveedoresTerrenoController(IProveedorTerrenoService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? activo)
        => Ok(await _service.ListAsync(activo));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProveedorTerrenoUpsertDto dto)
        => Ok(new { IdProveedorTerreno = await _service.UpsertAsync(dto) });

    [HttpPut("{proveedorId:int}")]
    public async Task<IActionResult> Update(int proveedorId, [FromBody] ProveedorTerrenoUpsertDto dto)
    {
        dto.IdProveedorTerreno = proveedorId;
        return Ok(new { IdProveedorTerreno = await _service.UpsertAsync(dto) });
    }

    [HttpDelete("{proveedorId:int}")]
    public async Task<IActionResult> Delete(int proveedorId)
    {
        await _service.DeleteAsync(proveedorId);
        return Ok(new { ok = true });
    }
}
