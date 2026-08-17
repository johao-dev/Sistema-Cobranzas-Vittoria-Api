using Cobranzas_Vittoria.Dtos.Almacen;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers
{
    [ApiController]
    [Route("api/almacen/kardex")]
    public class KardexController : ControllerBase
    {
        private readonly IKardexService _service;

        public KardexController(IKardexService service)
        {
            _service = service;
        }

        [HttpGet("movimientos")]
        public async Task<IActionResult> Movimientos(
            [FromQuery] int? idCompra,
            [FromQuery] int? idMaterial,
            [FromQuery] int? idEspecialidad,
            [FromQuery] string? fechaDesde,
            [FromQuery] string? fechaHasta)
        {
            var data = await _service.ListMovimientosAsync(idCompra, idMaterial, idEspecialidad, fechaDesde, fechaHasta);
            return Ok(data);
        }

        /// <summary>
        /// Endpoint legacy de registro de salidas manuales. Marcado con
        /// [Obsolete] en la Fase 3 del modulo Inventario y renombrado de
        /// <c>POST /salidas</c> a <c>POST /salidas-manuales</c> para liberar
        /// la ruta <c>/salidas</c> al nuevo <c>KardexInventarioController</c>.
        ///
        /// <para>
        /// <b>Por que se conserva y no se elimina</b>:
        /// el plan del modulo Inventario es ADITIVO. La logica legacy
        /// (que usa <c>almacen.KardexMovimiento</c> acoplado a
        /// <c>compras.CompraDetalle</c>) sigue siendo la unica que
        /// soporta salidas derivadas de Ordenes de Compra. Se eliminara
        /// unicamente cuando se confirme la migracion completa de todas
        /// las pantallas frontend al nuevo endpoint, en una fase posterior
        /// a la salida a produccion del KardexInventarioController.
        /// </para>
        ///
        /// <para>
        /// <b>Reemplazo</b>: usar
        /// <c>POST /api/almacen/kardex/salidas</c> del
        /// <c>KardexInventarioController</c> (ruta limpia, DTOs tipados,
        /// TVP, transaccionalidad del SP, traduccion 51100-51199 -> 422).
        /// </para>
        /// </summary>
        [Obsolete("Endpoint legacy. Use POST /api/almacen/kardex/salidas del KardexInventarioController (modulo Inventario / Kardex manual).")]
        [HttpPost("salidas-manuales")]
        public async Task<IActionResult> RegistrarSalidaManualLegacy([FromBody] KardexSalidaCreateDto dto)
            => Ok(await _service.RegistrarSalidaAsync(dto));
    }
}
