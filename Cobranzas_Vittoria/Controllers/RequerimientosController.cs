using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

[ApiController]
[Route("api/compras/requerimientos")]
public class RequerimientosController : ControllerBase
{
    private readonly IRequerimientoService _service;

    public RequerimientosController(IRequerimientoService service)
    {
        _service = service;
    }

    [HttpGet]
    [AuthorizePermission(Permisos.Requerimientos.Ver)]
    public async Task<IActionResult> List([FromQuery] string? estado, [FromQuery] int? idEspecialidad, [FromQuery] int? idProyecto)
        => Ok(await _service.ListAsync(estado, idEspecialidad, idProyecto));

    [HttpGet("{id:int}")]
    [AuthorizePermission(Permisos.Requerimientos.Ver)]
    public async Task<IActionResult> Get(int id)
    {
        var res = await _service.GetAsync(id);
        return res is null ? NotFound() : Ok(res);
    }

    [HttpPost]
    [AuthorizePermission(Permisos.Requerimientos.Crear)]
    public async Task<IActionResult> Crear([FromBody] RequerimientoCreateDto dto)
    {
        var id = await _service.CrearAsync(dto);
        return Ok(new { idRequerimiento = id });
    }

    [HttpPut("{id:int}")]
    [AuthorizePermission(Permisos.Requerimientos.EditarBorrador)]
    public async Task<IActionResult> Update(int id, [FromBody] RequerimientoUpdateDto dto)
    {
        await _service.UpdateAsync(id, dto);
        return Ok(new { ok = true });
    }

    [HttpPatch("{id:int}/estado")]
    [AuthorizePermission(Permisos.Requerimientos.EnviarCompras)]
    public async Task<IActionResult> UpdateEstado(int id, [FromBody] RequerimientoEstadoDto dto)
    {
        await _service.UpdateEstadoAsync(id, dto.Estado, dto.Observacion);
        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/validacion-almacen")]
    [AuthorizePermission(Permisos.Requerimientos.ProcesarStock)]
    public async Task<IActionResult> ValidarAlmacen(int id, [FromBody] RequerimientoValidacionDto dto)
    {
        await _service.ValidarAlmacenAsync(id, dto.IdUsuario, dto.Resultado, dto.Observacion);
        return Ok(new { ok = true });
    }
}
