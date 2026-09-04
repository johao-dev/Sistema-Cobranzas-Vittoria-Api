using Cobranzas_Vittoria.Application.Common.Excepciones;
using Cobranzas_Vittoria.Seguridad.Application.Usuario.Actualizar;
using Cobranzas_Vittoria.Seguridad.Application.Usuario.Crear;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Application.Common;

public sealed class UsuarioValidator
{
    public static void ValidarCreate(CreateUsuarioCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errores = new List<DetalleErrorValidacion>();

        if (string.IsNullOrWhiteSpace(command.Nombres))
        {
            errores.Add(new DetalleErrorValidacion(
                null,
                nameof(command.Nombres),
                "USUARIO_NOMBRE_REQUERIDO",
                "El nombre del usuario es requerido."));
        }

        if (string.IsNullOrWhiteSpace(command.Apellidos))
        {
            errores.Add(new DetalleErrorValidacion(
                null,
                nameof(command.Apellidos),
                "USUARIO_APELLIDO_REQUERIDO",
                "El apellido del usuario es requerido."));
        }

        if (string.IsNullOrWhiteSpace(command.Correo))
        {
            errores.Add(new DetalleErrorValidacion(
                null,
                nameof(command.Correo),
                "USUARIO_CORREO_REQUERIDO",
                "El correo del usuario es requerido."));
        }

        if (string.IsNullOrWhiteSpace(command.UsuarioLogin))
        {
            errores.Add(new DetalleErrorValidacion(
                null,
                nameof(command.UsuarioLogin),
                "USUARIO_LOGIN_REQUERIDO",
                "El usuario de login es requerido."));
        }

        if (string.IsNullOrWhiteSpace(command.PasswordHash))
        {
            errores.Add(new DetalleErrorValidacion(
                null,
                nameof(command.PasswordHash),
                "USUARIO_PASSWORD_REQUERIDO",
                "La contraseña es requerida."));
        }

        if (errores.Count > 0)
            throw new ValidacionNegocioSeguridadException(errores);
    }

    public static void ValidarUpdate(ActualizarUsuarioCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errores = new List<DetalleErrorValidacion>();

        if (command.IdUsuario <= 0)
        {
            errores.Add(new DetalleErrorValidacion(
                null,
                nameof(command.IdUsuario),
                "USUARIO_ID_INVALIDO",
                "El identificador del usuario no es valido."));
        }

        if (errores.Count > 0)
            throw new ValidacionNegocioSeguridadException(errores);
    }
}
