using Cobranzas_Vittoria.Dtos.Valorizaciones;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers
{
    [ApiController]
    [Route("api/contable/valorizaciones")]
    public class ValorizacionesController : ControllerBase
    {
        private readonly IValorizacionService _service;

        public ValorizacionesController(IValorizacionService service)
        {
            _service = service;
        }

        [HttpGet("configuraciones")]
        public async Task<IActionResult> ListConfiguraciones([FromQuery] int? idProyecto, [FromQuery] int? idProveedor, [FromQuery] int? idEspecialidad)
            => Ok(await _service.ListConfiguracionesAsync(idProyecto, idProveedor, idEspecialidad));

        [HttpPost("configuraciones")]
        public async Task<IActionResult> UpsertConfiguracion([FromBody] ProveedorEspecialidadCotizacionUpsertDto dto)
            => Ok(await _service.UpsertConfiguracionAsync(dto));

        [HttpPost("reglas-proveedor")]
        public async Task<IActionResult> UpsertReglaProveedor([FromBody] ProveedorReglaValorizacionUpsertDto dto)
            => Ok(await _service.UpsertReglaProveedorAsync(dto));

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int? idProyecto, [FromQuery] int? idProveedor, [FromQuery] int? idEspecialidad)
            => Ok(await _service.ListAsync(idProyecto, idProveedor, idEspecialidad));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
            => Ok(await _service.GetByIdAsync(id));

        [HttpPost]
        public async Task<IActionResult> Upsert([FromBody] ValorizacionUpsertDto dto)
            => Ok(await _service.UpsertAsync(dto));

        [HttpPost("detalle")]
        public async Task<IActionResult> UpsertDetalle([FromBody] ValorizacionDetalleUpsertDto dto)
            => Ok(await _service.UpsertDetalleAsync(dto));

        [HttpDelete("detalle/{id:int}")]
        public async Task<IActionResult> DeleteDetalle(int id, [FromQuery] string usuario = "system")
            => Ok(await _service.DeleteDetalleAsync(id, usuario));
    }
}
