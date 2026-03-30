using Cobranzas_Vittoria.Dtos.Maestra;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers
{
    [ApiController]
    [Route("api/maestra/unidades-medida")]
    public class UnidadMedidaController : ControllerBase
    {
        private readonly IUnidadMedidaService _service;

        public UnidadMedidaController(IUnidadMedidaService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] bool? activo)
        {
            var result = await _service.ListAsync(activo);
            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] UnidadMedidaUpsertDto dto)
        {
            var unidadId = await _service.UpsertAsync(dto);
            return Ok(new { idUnidadMedida = unidadId });
        }

        [HttpPut("{unidadId:int}")]
        public async Task<IActionResult> Update(int unidadId, [FromBody] UnidadMedidaUpsertDto dto)
        {
            dto.IdUnidadMedida = unidadId;
            var updatedId = await _service.UpsertAsync(dto);
            return Ok(new { idUnidadMedida = updatedId });
        }
    }
}
