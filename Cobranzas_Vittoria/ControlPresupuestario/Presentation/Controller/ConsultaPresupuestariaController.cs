using Cobranzas_Vittoria.Seguridad.Authorization;
using Cobranzas_Vittoria.ControlPresupuestario.Application.MovimientoPresupuestal.ListarConsolidados;
using Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.MovimientoPresupuestal;
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

/// <summary>Reportes por versión o con consolidación explícita e historial del ledger.</summary>
[ApiController]
[Route("api/control-presupuestario/consultas")]
[AuthorizePermission(Permisos.ControlPresupuestario.Reporte.Ver)]
public sealed class ConsultaPresupuestariaController : ControllerBase
{
    [HttpGet("resumen")]
    public async Task<IActionResult> Resumen([FromServices] ResumenHandler handler, [FromQuery] ReportePresupuestarioRequest filtro)
        => Ok(ResumenResponse.Desde(await handler.HandleAsync(filtro.AQuery())));

    [HttpGet("presupuesto-vs-comprometido")]
    public async Task<IActionResult> PresupuestoVsComprometido([FromServices] PresupuestoVsComprometidoHandler handler,
        [FromQuery] ReportePresupuestarioRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(PresupuestoVsComprometidoResponse.Desde));

    [HttpGet("presupuesto-vs-ejecutado")]
    public async Task<IActionResult> PresupuestoVsEjecutado([FromServices] PresupuestoVsEjecutadoHandler handler,
        [FromQuery] ReportePresupuestarioRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(PresupuestoVsEjecutadoResponse.Desde));

    [HttpGet("saldo")]
    public async Task<IActionResult> Saldo([FromServices] SaldoHandler handler, [FromQuery] ReportePresupuestarioRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(SaldoPartidaResponse.Desde));

    [HttpGet("gastos-por-partida")]
    public async Task<IActionResult> GastosPorPartida([FromServices] GastosPorPartidaHandler handler,
        [FromQuery] ReportePresupuestarioRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(GastoPorPartidaResponse.Desde));

    [HttpGet("gastos-por-centro-costo")]
    public async Task<IActionResult> GastosPorCentroCosto([FromServices] GastosPorCentroCostoHandler handler,
        [FromQuery] ReportePresupuestarioRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(GastoPorCentroCostoResponse.Desde));

    /// <summary>Historial APROBADO/HISTORICO por presupuesto y partida, con efectos netos.</summary>
    [HttpGet("movimientos")]
    [AuthorizePermission(Permisos.ControlPresupuestario.Movimiento.Ver)]
    public async Task<IActionResult> Movimientos([FromServices] ListarMovimientosConsolidadosHandler handler,
        [FromQuery] MovimientosConsolidadosRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(MovimientoConsolidadoResponse.Desde));

    [HttpGet("vigente")]
    public async Task<IActionResult> Vigente([FromServices] VigenteHandler handler, [FromQuery] ConsultaPresupuestariaRequest filtro)
        => Ok((await handler.HandleAsync(filtro.AQuery())).Select(SaldoPartidaResponse.Desde));

    /// <summary>
    /// Tablero de un centro de costo: distribución por rubro y curva acumulada real vs presupuesto.
    /// nivel agrupa los rubros en la partida ancestra de ese nivel de anidamiento (1 = raíces);
    /// idPartidaPadre limita todo el tablero a esa rama y, sin nivel, agrupa por sus hijas.
    /// partidasExcluidas acepta identificadores repetidos en query string y solo afecta a rubrosFiltrados.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard([FromServices] ObtenerDashboardHandler handler, [FromQuery] int idCentroCosto,
        [FromQuery] int? idPresupuesto, [FromQuery] int? nivel, [FromQuery] int? idPartidaPadre,
        [FromQuery] ExclusionPartidasRequest filtro)
        => Ok(await handler.HandleAsync(new ObtenerDashboardQuery(idCentroCosto, idPresupuesto, nivel, idPartidaPadre,
            filtro.PartidasExcluidas)));

    /// <summary>
    /// Árbol de partidas del centro de costo con subtotales por categoría (montos vigentes): lista plana
    /// en preorden; cada padre suma sus hojas descendientes.
    /// </summary>
    [HttpGet("arbol")]
    public async Task<IActionResult> Arbol([FromServices] ObtenerArbolVigenteHandler handler, [FromQuery] int idCentroCosto,
        [FromQuery] int? idPresupuesto)
        => Ok(await handler.HandleAsync(new ObtenerArbolVigenteQuery(idCentroCosto, idPresupuesto)));
}
