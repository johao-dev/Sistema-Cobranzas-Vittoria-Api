using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Cobranzas_Vittoria.Seguridad.Presentation.Dto;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Listar;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Actualizar;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.ObtenerPorId;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Habilitar;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Deshabilitar;

namespace Cobranzas_Vittoria.Seguridad.Presentation.Controller;

[ApiController]
[Authorize] // TODO: Implementar autorización solo para administradores
[Route("api/seguridad/permisos")]
public class PermisoController : ControllerBase
{
    private readonly ILogger<PermisoController> _logger;

    public PermisoController(ILogger<PermisoController> logger)
    {
        _logger = logger;
    }

    [HttpGet("{idPermiso}")]
    public async Task<IActionResult> GetById(
        [FromServices] ObtenerPorIdHandler handler,
        int idPermiso)
    {
        _logger.LogInformation("Consultando permiso por IdPermiso={IdPermiso}", idPermiso);

        ObtenerPorIdQuery query = new ObtenerPorIdQuery(idPermiso);
        ObtenerPorIdResult permiso = await handler.HandleAsync(query);

        PermisoResponse response = new PermisoResponse(
            permiso.IdPermiso,
            permiso.Codigo,
            permiso.Nombre,
            permiso.Descripcion,
            permiso.Activo,
            permiso.FechaCreacion,
            permiso.UsuarioCreacion,
            permiso.FechaModificacion,
            permiso.UsuarioModificacion);

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromServices] ListarPermisoHandler handler,
        [FromQuery] bool? activo)
    {
        _logger.LogDebug("Listando permisos. Filtro activo={Activo}", activo ?? true);

        ListarPermisoQuery query = new ListarPermisoQuery(activo ?? true);
        IEnumerable<ListarPermisoResult> permisos = await handler.HandleAsync(query);
        ListarPermisoResponse response = new ListarPermisoResponse(
            permisos.Select(p => new PermisoResponse(
                p.IdPermiso,
                p.Codigo,
                p.Nombre,
                p.Descripcion,
                p.Activo,
                p.FechaCreacion,
                p.UsuarioCreacion,
                p.FechaModificacion,
                p.UsuarioModificacion)).ToList());

        return Ok(response);
    }

    [HttpPut("{idPermiso}")]
    public async Task<IActionResult> Update(
        [FromServices] UpdatePermisoHandler handler,
        int idPermiso,
        [FromBody] UpdatePermisoRequest request)
    {
        _logger.LogInformation(
            "Solicitud de actualizacion de permiso: IdPermiso={IdPermiso}",
            idPermiso);
        _logger.LogDebug("Request de actualizacion: {@Request}", request);

        var command = new UpdatePermisoCommand(
            idPermiso,
            request.Nombre,
            request.Descripcion);

        await handler.HandleAsync(command);
        return NoContent();
    }

    [HttpPatch("{idPermiso}/enable")]
    public async Task<IActionResult> Enable(
        [FromServices] EnablePermisoHandler handler,
        [Range(1, int.MaxValue, ErrorMessage = "El identificador del permiso debe ser mayor que cero.")]
        int idPermiso)
    {
        await handler.HandleAsync(new EnablePermisoCommand(idPermiso));
        return NoContent();
    }

    [HttpPatch("{idPermiso}/disable")]
    public async Task<IActionResult> Disable(
        [FromServices] DisablePermisoHandler handler,
        [Range(1, int.MaxValue, ErrorMessage = "El identificador del permiso debe ser mayor que cero.")]
        int idPermiso)
    {
        await handler.HandleAsync(new DisablePermisoCommand(idPermiso));
        return NoContent();
    }
}
