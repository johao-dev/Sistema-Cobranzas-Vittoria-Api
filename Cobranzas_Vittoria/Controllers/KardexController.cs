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

        [HttpPost("salidas")]
        public async Task<IActionResult> RegistrarSalida([FromBody] KardexSalidaCreateDto dto)
            => Ok(await _service.RegistrarSalidaAsync(dto));
    }
}
