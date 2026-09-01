using Microsoft.AspNetCore.Mvc;
using Cobranzas_Vittoria.Seguridad.Presentation.Dto;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Crear;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Listar;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Actualizar;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Eliminar;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.ObtenerPorId;

namespace Cobranzas_Vittoria.Seguridad.Presentation.Controller;

[ApiController]
[Route("api/seguridad/permisos")]
public class PermisoController : ControllerBase
{
    private readonly CreatePermisoHandler _createPermisoHandler;
    private readonly ListarPermisoHandler _listarPermisoHandler;
    private readonly UpdatePermisoHandler _updatePermisoHandler;
    private readonly DeletePermisoHandler _deletePermisoHandler;
    private readonly ObtenerPorIdHandler _obtenerPorIdHandler;
    private readonly ILogger<PermisoController> _logger;

    public PermisoController(
        CreatePermisoHandler createPermisoHandler,
        ListarPermisoHandler listarPermisoHandler,
        UpdatePermisoHandler updatePermisoHandler,
        DeletePermisoHandler deletePermisoHandler,
        ObtenerPorIdHandler obtenerPorIdHandler,
        ILogger<PermisoController> logger)
    {
        _createPermisoHandler = createPermisoHandler;
        _listarPermisoHandler = listarPermisoHandler;
        _updatePermisoHandler = updatePermisoHandler;
        _deletePermisoHandler = deletePermisoHandler;
        _obtenerPorIdHandler = obtenerPorIdHandler;
        _logger = logger;
    }

    [HttpGet("{idPermiso}")]
    public async Task<IActionResult> GetById(int idPermiso)
    {
        _logger.LogInformation("Consultando permiso por IdPermiso={IdPermiso}", idPermiso);

        ObtenerPorIdQuery query = new ObtenerPorIdQuery(idPermiso);
        ObtenerPorIdResult permiso = await _obtenerPorIdHandler.HandleAsync(query);

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
    public async Task<IActionResult> GetAll([FromQuery] bool? activo)
    {
        _logger.LogDebug("Listando permisos. Filtro activo={Activo}", activo ?? true);

        ListarPermisoQuery query = new ListarPermisoQuery(activo ?? true);
        IEnumerable<ListarPermisoResult> permisos = await _listarPermisoHandler.HandleAsync(query);
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

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CreatePermisoRequest request)
    {
        _logger.LogInformation("Solicitud de creacion de permiso recibida");
        _logger.LogDebug("Request de creacion: {@Request}", request);

        CreatePermisoCommand command = new CreatePermisoCommand(
            request.Codigo,
            request.Nombre,
            request.Descripcion);

        CreatePermisoResult result = await _createPermisoHandler.HandleAsync(command);
        PermisoResponse response = new PermisoResponse(
            result.IdPermiso,
            result.Codigo,
            result.Nombre,
            result.Descripcion,
            result.Activo,
            result.FechaCreacion,
            result.UsuarioCreacion,
            null,
            null);

        _logger.LogInformation(
            "Permiso creado desde controller: IdPermiso={IdPermiso}, Codigo={Codigo}",
            response.IdPermiso,
            response.Codigo);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{idPermiso}")]
    public async Task<IActionResult> Update(int idPermiso, [FromBody] UpdatePermisoRequest request)
    {
        _logger.LogInformation(
            "Solicitud de actualizacion de permiso: IdPermiso={IdPermiso}",
            idPermiso);
        _logger.LogDebug("Request de actualizacion: {@Request}", request);

        var command = new UpdatePermisoCommand(
            idPermiso,
            request.Nombre,
            request.Descripcion);

        await _updatePermisoHandler.HandleAsync(command);
        return NoContent();
    }

    [HttpDelete("{idPermiso}")]
    public async Task<IActionResult> Delete(int idPermiso)
    {
        _logger.LogWarning(
            "Solicitud de eliminacion de permiso: IdPermiso={IdPermiso}",
            idPermiso);

        await _deletePermisoHandler.HandleAsync(new DeletePermisoCommand(idPermiso));
        return NoContent();
    }
}