using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.QuitarRol;

public class QuitarRolHandler
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ILogger<QuitarRolHandler> _logger;

    public QuitarRolHandler(
        IUsuarioRepository usuarioRepository,
        ILogger<QuitarRolHandler> logger)
    {
        _usuarioRepository = usuarioRepository;
        _logger = logger;
    }

    public async Task HandleAsync(QuitarRolCommand command)
    {
        _logger.LogInformation(
            "Iniciando eliminacion del rol IdRol={IdRol} del usuario IdUsuario={IdUsuario}",
            command.IdRol,
            command.IdUsuario);

        Domain.Model.Usuario? usuario = await _usuarioRepository.GetByIdAsync(command.IdUsuario)
            ?? throw new ValidacionNegocioSeguridadException(
                nameof(command.IdUsuario),
                "USUARIO_NO_ENCONTRADO",
                $"No se encontro el usuario con Id {command.IdUsuario}.");

        await _usuarioRepository.QuitarRolAsync(command.IdUsuario, command.IdRol);

        _logger.LogInformation(
            "Rol IdRol={IdRol} removido del usuario IdUsuario={IdUsuario}",
            command.IdRol,
            usuario.IdUsuario);
    }
}
