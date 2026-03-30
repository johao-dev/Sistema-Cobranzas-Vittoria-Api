using Cobranzas_Vittoria.Dtos.GastosAdministrativos;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

[ApiController]
[Route("api/maestra/proveedores-gasto")]
public class ProveedoresGastoController : ControllerBase
{
    private readonly IProveedorGastoAdministrativoService _service;
    public ProveedoresGastoController(IProveedorGastoAdministrativoService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool? activo, [FromQuery] int? idCategoriaGasto)
        => Ok(await _service.ListAsync(activo, idCategoriaGasto));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProveedorGastoAdministrativoUpsertDto dto)
        => Ok(new { IdProveedorGastoAdministrativo = await _service.UpsertAsync(dto) });

    [HttpPut("{proveedorGastoId:int}")]
    public async Task<IActionResult> Update(int proveedorGastoId, [FromBody] ProveedorGastoAdministrativoUpsertDto dto)
    {
        dto.IdProveedorGastoAdministrativo = proveedorGastoId;
        return Ok(new { IdProveedorGastoAdministrativo = await _service.UpsertAsync(dto) });
    }

    [HttpDelete("{proveedorGastoId:int}")]
    public async Task<IActionResult> Delete(int proveedorGastoId)
    {
        await _service.DeleteAsync(proveedorGastoId);
        return Ok(new { ok = true });
    }
}
