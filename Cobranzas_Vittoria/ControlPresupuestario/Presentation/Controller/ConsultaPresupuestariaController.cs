using Cobranzas_Vittoria.Seguridad.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Arbol;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Dashboard;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.GastosPorCentroCosto;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.GastosPorPartida;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.PresupuestoVsComprometido;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.PresupuestoVsEjecutado;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Resumen;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Saldo;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Vigente;
using Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.ConsultaPresupuestaria;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Controller;

/// <summary>Consultas de reporting: cada una consume directamente una vista existente.</summary>
[ApiController]
[Route("api/control-presupuestario/consultas")]
[AuthorizePermission(Permisos.ControlPresupuestario.Reporte.Ver)]
public sealed class ConsultaPresupuestariaController : ControllerBase
{
    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen([FromServices] ResumenHandler handler, [FromQuery] ConsultaPresupuestariaRequest filtro)
        => Ok(ResumenResponse.Desde(await handler.HandleAsync(filtro.AQuery())));

    [HttpGet("presupuesto-vs-comprometido")]
    public async Task<IActionResult> PresupuestoVsComprometido([FromServices] PresupuestoVsComprometidoHandler handler,
        [FromQuery] ConsultaPresupuestariaRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(PresupuestoVsComprometidoResponse.Desde));

    [HttpGet("presupuesto-vs-ejecutado")]
    public async Task<IActionResult> PresupuestoVsEjecutado([FromServices] PresupuestoVsEjecutadoHandler handler,
        [FromQuery] ConsultaPresupuestariaRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(PresupuestoVsEjecutadoResponse.Desde));

    [HttpGet("saldo")]
    public async Task<IActionResult> Saldo([FromServices] SaldoHandler handler, [FromQuery] ConsultaPresupuestariaRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(SaldoPartidaResponse.Desde));

    [HttpGet("gastos-por-partida")]
    public async Task<IActionResult> GastosPorPartida([FromServices] GastosPorPartidaHandler handler,
        [FromQuery] ConsultaPresupuestariaRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(GastoPorPartidaResponse.Desde));

    [HttpGet("gastos-por-centro-costo")]
    public async Task<IActionResult> GastosPorCentroCosto([FromServices] GastosPorCentroCostoHandler handler,
        [FromQuery] ConsultaPresupuestariaRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(GastoPorCentroCostoResponse.Desde));

    [HttpGet("vigente")]
    public async Task<IActionResult> Vigente([FromServices] VigenteHandler handler, [FromQuery] ConsultaPresupuestariaRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(SaldoPartidaResponse.Desde));

    /// <summary>
    /// Tablero de un centro de costo: distribución por rubro y curva acumulada real vs presupuesto.
    /// nivel agrupa los rubros en la partida ancestra de ese nivel de anidamiento (1 = raíces);
    /// idPartidaPadre limita todo el tablero a esa rama y, sin nivel, agrupa por sus hijas.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromServices] ObtenerDashboardHandler handler, [FromQuery] int idCentroCosto,
        [FromQuery] int? idPresupuesto, [FromQuery] int? nivel, [FromQuery] int? idPartidaPadre)
        => Ok(await handler.HandleAsync(new ObtenerDashboardQuery(idCentroCosto, idPresupuesto, nivel, idPartidaPadre)));

    /// <summary>
    /// Árbol de partidas del centro de costo con subtotales por categoría (montos vigentes): lista plana
    /// en preorden; cada padre suma sus hojas descendientes.
    /// </summary>
    [HttpGet("arbol")]
    public async Task<IActionResult> Arbol([FromServices] ObtenerArbolVigenteHandler handler, [FromQuery] int idCentroCosto,
        [FromQuery] int? idPresupuesto)
        => Ok(await handler.HandleAsync(new ObtenerArbolVigenteQuery(idCentroCosto, idPresupuesto)));
}
