using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Dtos.Compras.Requerimientos;
using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

[ApiController]
[Route("api/compras/requerimientos")]
public class RequerimientosController : ControllerBase
{
    private readonly IRequerimientoService _service;
    private readonly IUsuarioActualService _usuarioActual;

    public RequerimientosController(IRequerimientoService service, IUsuarioActualService usuarioActual)
    {
        _service = service;
        _usuarioActual = usuarioActual;
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
        dto.IdUsuarioSolicitante = ObtenerIdUsuarioActual();
        var id = await _service.CrearAsync(dto);
        return Ok(new { idRequerimiento = id });
    }

    [HttpPut("{id:int}")]
    [AuthorizePermission(Permisos.Requerimientos.EditarBorrador)]
    public async Task<IActionResult> Update(int id, [FromBody] RequerimientoUpdateDto dto)
    {
        dto.IdUsuarioSolicitante = ObtenerIdUsuarioActual();
        await _service.UpdateAsync(id, dto.IdUsuarioSolicitante, dto);
        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/enviar")]
    [AuthorizePermission(Permisos.Requerimientos.Enviar)]
    public async Task<IActionResult> Enviar(int id, [FromBody] EnviarRequerimientoRequest request)
    {
        await _service.EnviarAsync(id, ObtenerIdUsuarioActual(), request.Observacion);
        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/validacion-almacen")]
    [AuthorizePermission(Permisos.Requerimientos.ProcesarStock)]
    public async Task<IActionResult> ProcesarStock(int id, [FromBody] ProcesarStockRequerimientoRequest request)
    {
        await _service.ProcesarStockAsync(id, ObtenerIdUsuarioActual(), request.Resultado, request.Observacion);
        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/aprobar")]
    [AuthorizePermission(Permisos.Requerimientos.Aprobar)]
    public async Task<IActionResult> Aprobar(int id, [FromBody] AprobarRequerimientoRequest request)
    {
        await _service.AprobarAsync(id, ObtenerIdUsuarioActual(), request.Observacion);
        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/rechazar")]
    [AuthorizePermission(Permisos.Requerimientos.Rechazar)]
    public async Task<IActionResult> Rechazar(int id, [FromBody] RechazarRequerimientoRequest request)
    {
        await _service.RechazarAsync(id, ObtenerIdUsuarioActual(), request.Observacion);
        return Ok(new { ok = true });
    }

    [HttpPost("{id:int}/enviar-compras")]
    [AuthorizePermission(Permisos.Requerimientos.EnviarCompras)]
    public async Task<IActionResult> EnviarCompras(int id, [FromBody] EnviarRequerimientoComprasRequest request)
    {
        await _service.EnviarComprasAsync(id, ObtenerIdUsuarioActual(), request.Observacion);
        return Ok(new { ok = true });
    }

    private int ObtenerIdUsuarioActual() => _usuarioActual.IdUsuario
        ?? throw new AutenticacionException();
}
