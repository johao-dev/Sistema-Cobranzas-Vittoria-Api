using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers
{
    [ApiController]
    [Route("api/maestra/materiales")]
    public class MaterialesController : ControllerBase
    {
        private readonly IMaterialService _service;
        public MaterialesController(IMaterialService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] bool? activo, [FromQuery] int? idEspecialidad)
            => Ok(await _service.ListAsync(activo, idEspecialidad));

        [HttpGet("siguiente-codigo")]
        public async Task<IActionResult> SiguienteCodigo()
            => Ok(new { Codigo = await _service.GetSiguienteCodigoAsync() });

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            var res = await _service.GetAsync(id);
            return res is null ? NotFound() : Ok(res);
        }

        [HttpPost]
        public async Task<IActionResult> Upsert([FromBody] MaterialUpsertDto dto)
        {
            var id = await _service.UpsertAsync(dto);
            return Ok(new { IdMaterial = id });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] MaterialUpsertDto dto)
        {
            dto.IdMaterial = id;
            var materialId = await _service.UpsertAsync(dto);
            return Ok(new { IdMaterial = materialId });
        }
    }
}
