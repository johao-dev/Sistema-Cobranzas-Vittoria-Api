using Microsoft.AspNetCore.Mvc;
using Cobranzas_Vittoria.Seguridad.Presentation.Dto;
using Cobranzas_Vittoria.Seguridad.Application.Rol.Crear;
using Cobranzas_Vittoria.Seguridad.Application.Rol.Listar;
using Cobranzas_Vittoria.Seguridad.Application.Rol.Actualizar;
using Cobranzas_Vittoria.Seguridad.Application.Rol.Obtener;

namespace Cobranzas_Vittoria.Seguridad.Presentation.Controller;

[ApiController]
[Route("api/seguridad/roles")]
public class RolController : ControllerBase
{
    private readonly CreateRolHandler _createRolHandler;
    private readonly ListarRolHandler _listarRolHandler;
    private readonly ActualizarRolHandler _actualizarRolHandler;
    private readonly ObtenerRolHandler _obtenerRolHandler;
    private readonly ILogger<RolController> _logger;

    public RolController(
        CreateRolHandler createRolHandler,
        ListarRolHandler listarRolHandler,
        ActualizarRolHandler actualizarRolHandler,
        ObtenerRolHandler obtenerRolHandler,
        ILogger<RolController> logger)
    {
        _createRolHandler = createRolHandler;
        _listarRolHandler = listarRolHandler;
        _actualizarRolHandler = actualizarRolHandler;
        _obtenerRolHandler = obtenerRolHandler;
        _logger = logger;
    }

    [HttpGet("{idRol}")]
    public async Task<IActionResult> GetById(int idRol)
    {
        _logger.LogInformation("Consultando rol por IdRol={IdRol}", idRol);

        ObtenerRolQuery query = new ObtenerRolQuery(idRol);
        ObtenerRolResult rol = await _obtenerRolHandler.HandleAsync(query);

        RolResponse response = new RolResponse(
            rol.IdRol,
            rol.Nombre,
            rol.Descripcion,
            rol.Activo,
            rol.FechaCreacion,
            rol.UsuarioCreacion,
            rol.FechaModificacion,
            rol.UsuarioModificacion);

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activo)
    {
        _logger.LogDebug("Listando roles. Filtro activo={Activo}", activo ?? true);

        ListarRolQuery query = new ListarRolQuery(activo ?? true);
        IEnumerable<ListarRolResult> roles = await _listarRolHandler.HandleAsync(query);
        ListarRolResponse response = new ListarRolResponse(
            roles.Select(r => new RolResponse(
                r.IdRol,
                r.Nombre,
                r.Descripcion,
                r.Activo,
                r.FechaCreacion,
                r.UsuarioCreacion,
                r.FechaModificacion,
                r.UsuarioModificacion)).ToList());

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CreateRolRequest request)
    {
        _logger.LogInformation("Solicitud de creacion de rol recibida");
        _logger.LogDebug("Request de creacion: {@Request}", request);

        CreateRolCommand command = new CreateRolCommand(
            request.Nombre,
            request.Descripcion);

        CreateRolResult result = await _createRolHandler.HandleAsync(command);
        RolResponse response = new RolResponse(
            result.IdRol,
            result.Nombre,
            result.Descripcion,
            result.Activo,
            result.FechaCreacion,
            result.UsuarioCreacion,
            null,
            null);

        _logger.LogInformation(
            "Rol creado desde controller: IdRol={IdRol}, Nombre={Nombre}",
            response.IdRol,
            response.Nombre);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{idRol}")]
    public async Task<IActionResult> Update(int idRol, [FromBody] UpdateRolRequest request)
    {
        _logger.LogInformation(
            "Solicitud de actualizacion de rol: IdRol={IdRol}",
            idRol);
        _logger.LogDebug("Request de actualizacion: {@Request}", request);

        var command = new ActualizarRolCommand(
            idRol,
            request.Nombre,
            request.Descripcion,
            request.Activo);

        await _actualizarRolHandler.HandleAsync(command);
        return NoContent();
    }
}