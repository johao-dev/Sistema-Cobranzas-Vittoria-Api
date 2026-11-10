using Cobranzas_Vittoria.Compras.Application.Requerimiento.Crear;
using Cobranzas_Vittoria.Compras.Presentation.Dto;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Compras.Presentation.Controller;

[ApiController]
[Route("api/compras/requerimientos")]
public sealed class RequerimientoCreacionController : ControllerBase
{
    [HttpPost]
    [AuthorizePermission(Permisos.Requerimientos.Crear)]
    public async Task<IActionResult> Crear([FromBody] CrearRequerimientoRequest request,
        [FromServices] CrearRequerimientoHandler handler, CancellationToken ct)
    {
        CrearRequerimientoResult result = await handler.HandleAsync(request.ToCommand(), ct);
        return Ok(new CrearRequerimientoResponse(result.IdRequerimiento));
    }
}
