using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers
{
    [ApiController]
    [Route("api/maestra/proveedores")]
    public class ProveedoresController : ControllerBase
    {
        private readonly IProveedorService _service;
        private readonly ISunatService _sunatService;

        public ProveedoresController(IProveedorService service, ISunatService sunatService)
        {
            _service = service;
            _sunatService = sunatService;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] bool? activo, [FromQuery] int? idEspecialidad)
            => Ok(await _service.ListAsync(activo, idEspecialidad));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var res = await _service.GetAsync(id);
            return res is null ? NotFound() : Ok(res);
        }

        [HttpGet("consulta-ruc/{ruc}")]
        public async Task<IActionResult> GetPorRuc(string ruc)
        {
            if (string.IsNullOrWhiteSpace(ruc) || ruc.Length != 11)
                return BadRequest("El RUC debe tener 11 digitos.");

            var datosSunat = await _sunatService.ConsultarRucAsync(ruc);

            if (datosSunat == null)
                return NotFound("No se encontraron datos para el RUC ingresado.");

            return Ok(datosSunat);
        }

        [HttpPost]
        public async Task<IActionResult> Upsert([FromBody] ProveedorUpsertDto dto)
        {
            var id = await _service.UpsertAsync(dto);
            return Ok(new { IdProveedor = id });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] ProveedorUpsertDto dto)
        {
            dto.IdProveedor = id;
            var provId = await _service.UpsertAsync(dto);
            return Ok(new { IdProveedor = provId });
        }

        [HttpPost("{id:int}/especialidades")]
        public async Task<IActionResult> SetEspecialidad(int id, [FromBody] ProveedorEspecialidadDto dto)
        {
            await _service.SetEspecialidadAsync(id, dto.IdEspecialidad, dto.Activo);
            return Ok(new { Ok = true });
        }
    }
}
