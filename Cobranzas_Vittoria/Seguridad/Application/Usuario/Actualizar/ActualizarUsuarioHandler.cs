using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.Actualizar;

public class ActualizarUsuarioHandler
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ILogger<ActualizarUsuarioHandler> _logger;

    public ActualizarUsuarioHandler(
        IUsuarioRepository usuarioRepository,
        ILogger<ActualizarUsuarioHandler> logger)
    {
        _usuarioRepository = usuarioRepository;
        _logger = logger;
    }

    public async Task<ActualizarUsuarioResult> HandleAsync(ActualizarUsuarioCommand command)
    {
        _logger.LogInformation(
            "Iniciando actualizacion del usuario IdUsuario={IdUsuario}",
            command.IdUsuario);
        _logger.LogDebug("Datos recibidos para actualizar usuario: {@Command}", command);

        UsuarioValidator.ValidarUpdate(command);

        Domain.Model.Usuario? usuario = await _usuarioRepository.GetByIdAsync(command.IdUsuario)
            ?? throw new ValidacionNegocioSeguridadException(
                nameof(command.IdUsuario),
                "USUARIO_NO_ENCONTRADO",
                $"No se encontro el usuario con Id {command.IdUsuario}.");

        if (!string.IsNullOrWhiteSpace(command.Correo))
        {
            var correoActual = command.Correo.Trim();
            var usuarioConCorreo = await _usuarioRepository.GetByCorreoAsync(correoActual);
            if (usuarioConCorreo is not null && usuarioConCorreo.IdUsuario != command.IdUsuario)
            {
                throw new ValidacionNegocioSeguridadException(
                    nameof(command.Correo),
                    "USUARIO_CORREO_DUPLICADO",
                    $"Ya existe otro usuario con el correo {correoActual}.");
            }

            usuario.ActualizarCorreo(correoActual);
        }

        if (!string.IsNullOrWhiteSpace(command.UsuarioLogin) || !string.IsNullOrWhiteSpace(command.PasswordHash))
        {
            usuario.AsignarCredenciales(
                command.UsuarioLogin ?? usuario.UsuarioLogin,
                command.PasswordHash ?? usuario.PasswordHash);
        }

        usuario.ActualizarDatos(
            command.Nombres ?? usuario.Nombres,
            command.Apellidos ?? usuario.Apellidos);

        if (command.Activo.HasValue)
        {
            if (command.Activo.Value)
            {
                usuario.Activar();
            }
            else
            {
                usuario.Desactivar();
            }
        }

        Domain.Model.Usuario actualizado = await _usuarioRepository.UpdateAsync(usuario);

        _logger.LogInformation(
            "Usuario actualizado exitosamente: IdUsuario={IdUsuario}",
            actualizado.IdUsuario);

        return new ActualizarUsuarioResult(
            actualizado.IdUsuario,
            actualizado.Nombres,
            actualizado.Apellidos,
            actualizado.Correo.Value,
            actualizado.UsuarioLogin,
            actualizado.Activo);
    }
}
