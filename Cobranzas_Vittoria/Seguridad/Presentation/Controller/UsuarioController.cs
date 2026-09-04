using Microsoft.AspNetCore.Mvc;
using Cobranzas_Vittoria.Seguridad.Presentation.Dto;
using Cobranzas_Vittoria.Seguridad.Application.Usuario.Crear;
using Cobranzas_Vittoria.Seguridad.Application.Usuario.Listar;
using Cobranzas_Vittoria.Seguridad.Application.Usuario.Obtener;
using Cobranzas_Vittoria.Seguridad.Application.Usuario.Actualizar;
using Cobranzas_Vittoria.Seguridad.Application.Usuario.AsignarRoles;
using Cobranzas_Vittoria.Seguridad.Application.Usuario.QuitarRol;

namespace Cobranzas_Vittoria.Seguridad.Presentation.Controller;

[ApiController]
[Route("api/seguridad/usuarios")]
public class UsuarioController : ControllerBase
{
    private readonly CreateUsuarioHandler _createUsuarioHandler;
    private readonly ListarUsuarioHandler _listarUsuarioHandler;
    private readonly ObtenerUsuarioHandler _obtenerUsuarioHandler;
    private readonly ActualizarUsuarioHandler _actualizarUsuarioHandler;
    private readonly AsignarRolesHandler _asignarRolesHandler;
    private readonly QuitarRolHandler _quitarRolHandler;
    private readonly ILogger<UsuarioController> _logger;

    public UsuarioController(
        CreateUsuarioHandler createUsuarioHandler,
        ListarUsuarioHandler listarUsuarioHandler,
        ObtenerUsuarioHandler obtenerUsuarioHandler,
        ActualizarUsuarioHandler actualizarUsuarioHandler,
        AsignarRolesHandler asignarRolesHandler,
        QuitarRolHandler quitarRolHandler,
        ILogger<UsuarioController> logger)
    {
        _createUsuarioHandler = createUsuarioHandler;
        _listarUsuarioHandler = listarUsuarioHandler;
        _obtenerUsuarioHandler = obtenerUsuarioHandler;
        _actualizarUsuarioHandler = actualizarUsuarioHandler;
        _asignarRolesHandler = asignarRolesHandler;
        _quitarRolHandler = quitarRolHandler;
        _logger = logger;
    }

    [HttpGet("{idUsuario}")]
    public async Task<IActionResult> GetById(int idUsuario)
    {
        _logger.LogInformation("Consultando usuario por IdUsuario={IdUsuario}", idUsuario);

        ObtenerUsuarioQuery query = new ObtenerUsuarioQuery(idUsuario);
        ObtenerUsuarioResult usuario = await _obtenerUsuarioHandler.HandleAsync(query);

        UsuarioResponse response = new UsuarioResponse(
            usuario.IdUsuario,
            usuario.Nombres,
            usuario.Apellidos,
            usuario.Correo,
            usuario.UsuarioLogin,
            usuario.Activo,
            usuario.FechaCreacion,
            usuario.UsuarioCreacion);

        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool? activo)
    {
        _logger.LogDebug("Listando usuarios. Filtro activo={Activo}", activo ?? true);

        ListarUsuarioQuery query = new ListarUsuarioQuery(activo ?? true);
        IEnumerable<ListarUsuarioResult> usuarios = await _listarUsuarioHandler.HandleAsync(query);
        ListarUsuarioResponse response = new ListarUsuarioResponse(
            usuarios.Select(u => new UsuarioResponse(
                u.IdUsuario,
                u.Nombres,
                u.Apellidos,
                u.Correo,
                u.UsuarioLogin,
                u.Activo,
                u.FechaCreacion,
                u.UsuarioCreacion)).ToList());

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] CreateUsuarioRequest request)
    {
        _logger.LogInformation("Solicitud de creacion de usuario recibida");
        _logger.LogDebug("Request de creacion: {@Request}", request);

        CreateUsuarioCommand command = new CreateUsuarioCommand(
            request.Nombres,
            request.Apellidos,
            request.Correo,
            request.UsuarioLogin,
            request.PasswordHash);

        CreateUsuarioResult result = await _createUsuarioHandler.HandleAsync(command);
        UsuarioResponse response = new UsuarioResponse(
            result.IdUsuario,
            result.Nombres,
            result.Apellidos,
            result.Correo,
            result.UsuarioLogin,
            result.Activo,
            result.FechaCreacion,
            result.UsuarioCreacion);

        _logger.LogInformation(
            "Usuario creado desde controller: IdUsuario={IdUsuario}, Login={UsuarioLogin}",
            response.IdUsuario,
            response.UsuarioLogin);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPut("{idUsuario}")]
    public async Task<IActionResult> Update(int idUsuario, [FromBody] UpdateUsuarioRequest request)
    {
        _logger.LogInformation(
            "Solicitud de actualizacion de usuario: IdUsuario={IdUsuario}",
            idUsuario);
        _logger.LogDebug("Request de actualizacion: {@Request}", request);

        var command = new ActualizarUsuarioCommand(
            idUsuario,
            request.Nombres,
            request.Apellidos,
            request.Correo,
            request.UsuarioLogin,
            request.PasswordHash,
            request.Activo);

        await _actualizarUsuarioHandler.HandleAsync(command);
        return NoContent();
    }

    [HttpPost("{idUsuario}/roles")]
    public async Task<IActionResult> AsignarRoles(int idUsuario, [FromBody] AsignarRolesRequest request)
    {
        _logger.LogInformation(
            "Solicitud de asignacion de roles al usuario IdUsuario={IdUsuario}",
            idUsuario);

        AsignarRolesCommand command = new AsignarRolesCommand(idUsuario, request.IdRoles);
        await _asignarRolesHandler.HandleAsync(command);
        return NoContent();
    }

    [HttpDelete("{idUsuario}/roles/{idRol}")]
    public async Task<IActionResult> QuitarRol(int idUsuario, int idRol)
    {
        _logger.LogInformation(
            "Solicitud de eliminacion del rol IdRol={IdRol} del usuario IdUsuario={IdUsuario}",
            idRol,
            idUsuario);

        QuitarRolCommand command = new QuitarRolCommand(idUsuario, idRol);
        await _quitarRolHandler.HandleAsync(command);
        return NoContent();
    }
}
