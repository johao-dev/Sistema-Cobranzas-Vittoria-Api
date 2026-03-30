using Cobranzas_Vittoria.Dtos.Seguridad;
using Cobranzas_Vittoria.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers
{
    [ApiController]
    [Route("api/seguridad/roles")]
    public class RolesController : ControllerBase
    {
        private readonly IRolService _service;
        public RolesController(IRolService service) => _service = service;

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] bool? activo) => Ok(await _service.ListAsync(activo));

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RolUpsertDto dto)
        {
            var rolId = await _service.UpsertAsync(dto);
            return Ok(new { idRol = rolId });
        }

        [HttpPut("{rolId:int}")]
        public async Task<IActionResult> Update(int rolId, [FromBody] RolUpsertDto dto)
        {
            dto.IdRol = rolId;
            var updatedId = await _service.UpsertAsync(dto);
            return Ok(new { idRol = updatedId });
        }
    }
}
