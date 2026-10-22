using Cobranzas_Vittoria.Inventario.Application.StockProyecto;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Cobranzas_Vittoria.Inventario.Presentation.Dto;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Inventario.Presentation.Controller;

[ApiController]
[Route("api/almacen")]
public sealed class StockProyectoController : ControllerBase
{
    private readonly IStockProyectoService _service;

    public StockProyectoController(IStockProyectoService service) => _service = service;

    /// <summary>Distribución actual. Las fechas filtran el último cambio de asignación.</summary>
    [HttpGet("kardex/stock-proyecto")]
    public async Task<ActionResult<IReadOnlyList<StockProyecto>>> ListarStock(
        [FromQuery] FiltroStockProyecto filtro, CancellationToken ct)
        => Ok(await _service.ListarStockAsync(filtro, ct));

    /// <summary>Transferencias con una fila por material consolidado.</summary>
    [HttpGet("transferencias-proyecto")]
    public async Task<ActionResult<IReadOnlyList<TransferenciaProyecto>>> ListarTransferencias(
        [FromQuery] FiltroTransferenciaProyecto filtro, CancellationToken ct)
        => Ok(await _service.ListarTransferenciasAsync(filtro, ct));

    [HttpGet("transferencias-proyecto/{id:int:min(1)}")]
    public async Task<ActionResult<IReadOnlyList<TransferenciaProyecto>>> ObtenerTransferencia(int id, CancellationToken ct)
        => Ok(await _service.ObtenerTransferenciaAsync(id, ct));

    /// <summary>Reasigna stock entre proyectos sin modificar el stock físico global.</summary>
    [HttpPost("transferencias-proyecto")]
    public async Task<ActionResult<IReadOnlyList<TransferenciaProyecto>>> RegistrarTransferencia(
        [FromBody] TransferenciaProyectoCreateDto dto, CancellationToken ct)
    {
        RegistroTransferenciaProyecto registro = new()
        {
            IdProyectoOrigen = dto.IdProyectoOrigen,
            IdProyectoDestino = dto.IdProyectoDestino,
            Fecha = dto.Fecha!.Value,
            Observacion = dto.Observacion!,
            Items = dto.Items.Select(item => item is null ? null! : new ItemTransferenciaProyecto
            {
                IdMaterial = item.IdMaterial,
                Cantidad = item.Cantidad
            }).ToList()
        };

        return Ok(await _service.RegistrarTransferenciaAsync(registro, ct));
    }
}
