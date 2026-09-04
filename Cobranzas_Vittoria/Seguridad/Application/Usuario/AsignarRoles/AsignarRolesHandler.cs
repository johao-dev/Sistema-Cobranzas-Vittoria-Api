using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.AsignarRoles;

public class AsignarRolesHandler
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ILogger<AsignarRolesHandler> _logger;

    public AsignarRolesHandler(
        IUsuarioRepository usuarioRepository,
        ILogger<AsignarRolesHandler> logger)
    {
        _usuarioRepository = usuarioRepository;
        _logger = logger;
    }

    public async Task HandleAsync(AsignarRolesCommand command)
    {
        _logger.LogInformation(
            "Iniciando asignacion de roles al usuario IdUsuario={IdUsuario}",
            command.IdUsuario);
        _logger.LogDebug("Roles a asignar: {@Roles}", command.IdRoles);

        Domain.Model.Usuario? usuario = await _usuarioRepository.GetByIdAsync(command.IdUsuario)
            ?? throw new ValidacionNegocioSeguridadException(
                nameof(command.IdUsuario),
                "USUARIO_NO_ENCONTRADO",
                $"No se encontro el usuario con Id {command.IdUsuario}.");

        var roles = command.IdRoles.ToList();
        if (roles.Count == 0)
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(command.IdRoles),
                "USUARIO_ROLES_REQUERIDOS",
                "Debe proporcionar al menos un rol para asignar.");
        }

        await _usuarioRepository.AsignarRolesAsync(command.IdUsuario, roles);

        _logger.LogInformation(
            "Roles asignados exitosamente al usuario IdUsuario={IdUsuario}",
            usuario.IdUsuario);
    }
}
