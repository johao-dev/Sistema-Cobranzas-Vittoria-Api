using Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Actualizar;
using Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Agregar;
using Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Eliminar;
using Cobranzas_Vittoria.Compras.Presentation.Dto;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Compras.Presentation.Controller;

[ApiController]
[Route("api/compras/requerimientos/{idRequerimiento:int}/items")]
[AuthorizePermission(Permisos.Requerimientos.GestionarItemsAlmacen)]
public sealed class RequerimientoItemController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Agregar(int idRequerimiento, [FromBody] AgregarItemRequerimientoRequest request,
        [FromServices] AgregarItemHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new AgregarItemCommand(idRequerimiento, request.IdMaterial,
            request.Cantidad!.Value, request.IdPresupuestoDetalle, request.Observacion), ct);
        return StatusCode(StatusCodes.Status201Created, RequerimientoItemResponse.Desde(result));
    }

    [HttpPatch("{idDetalle:int}")]
    public async Task<IActionResult> Actualizar(int idRequerimiento, int idDetalle,
        [FromBody] ActualizarItemRequerimientoRequest request, [FromServices] ActualizarItemHandler handler,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(new ActualizarItemCommand(idRequerimiento, idDetalle,
            request.Cantidad, request.IdPresupuestoDetalle, request.Observacion, request.Campos), ct);
        return Ok(RequerimientoItemResponse.Desde(result));
    }

    [HttpDelete("{idDetalle:int}")]
    public async Task<IActionResult> Eliminar(int idRequerimiento, int idDetalle,
        [FromServices] EliminarItemHandler handler, CancellationToken ct)
    {
        await handler.HandleAsync(new EliminarItemCommand(idRequerimiento, idDetalle), ct);
        return NoContent();
    }
}
