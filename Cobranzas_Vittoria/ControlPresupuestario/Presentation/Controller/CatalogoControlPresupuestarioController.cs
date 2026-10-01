using Cobranzas_Vittoria.Seguridad.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Cobranzas_Vittoria.ControlPresupuestario.Application.Catalogo.ListarEstadosPresupuesto;
using Cobranzas_Vittoria.ControlPresupuestario.Application.Catalogo.ListarMonedas;
using Cobranzas_Vittoria.ControlPresupuestario.Application.Catalogo.ListarSeccionesGasto;
using Cobranzas_Vittoria.ControlPresupuestario.Application.Catalogo.ListarTiposCentroCosto;
using Cobranzas_Vittoria.ControlPresupuestario.Application.Catalogo.ListarTiposMovimiento;
using Cobranzas_Vittoria.ControlPresupuestario.Application.Catalogo.ListarTiposPartida;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Controller;

/// <summary>
/// Catálogos auxiliares de lectura para la UI. No requieren permisos administrativos: basta una
/// sesión autenticada, porque forman parte del acceso normal al módulo.
/// </summary>
[ApiController]
[Authorize]
[Route("api/control-presupuestario/catalogos")]
public sealed class CatalogoControlPresupuestarioController : ControllerBase
{
    [HttpGet("estados-presupuesto")]
    public async Task<IActionResult> EstadosPresupuesto([FromServices] ListarEstadosPresupuestoHandler handler,
        [FromQuery] bool? activo = true)
        => Ok(await handler.HandleAsync(new ListarEstadosPresupuestoQuery(activo)));

    [HttpGet("tipos-centro-costo")]
    public async Task<IActionResult> TiposCentroCosto([FromServices] ListarTiposCentroCostoHandler handler,
        [FromQuery] bool? activo = true)
        => Ok(await handler.HandleAsync(new ListarTiposCentroCostoQuery(activo)));

    [HttpGet("tipos-partida")]
    public async Task<IActionResult> TiposPartida([FromServices] ListarTiposPartidaHandler handler, [FromQuery] bool? activo = true)
        => Ok(await handler.HandleAsync(new ListarTiposPartidaQuery(activo)));

    [HttpGet("tipos-movimiento")]
    public async Task<IActionResult> TiposMovimiento([FromServices] ListarTiposMovimientoHandler handler,
        [FromQuery] bool? activo = true)
        => Ok(await handler.HandleAsync(new ListarTiposMovimientoQuery(activo)));

    [HttpGet("monedas")]
    public async Task<IActionResult> Monedas([FromServices] ListarMonedasHandler handler, [FromQuery] bool? activo = true)
        => Ok(await handler.HandleAsync(new ListarMonedasQuery(activo)));

    [HttpGet("secciones-gasto")]
    public async Task<IActionResult> SeccionesGasto([FromServices] ListarSeccionesGastoHandler handler,
        [FromQuery] bool? activo = true)
        => Ok(await handler.HandleAsync(new ListarSeccionesGastoQuery(activo)));
}
