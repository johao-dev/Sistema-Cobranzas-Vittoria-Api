using Cobranzas_Vittoria.Application.Common.Excepciones;
using Cobranzas_Vittoria.Seguridad.Application.Rol.Actualizar;
using Cobranzas_Vittoria.Seguridad.Application.Rol.Crear;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Application.Common;

public sealed class RolValidator
{
    public static void ValidarCreate(CreateRolCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errores = new List<DetalleErrorValidacion>();

        if (string.IsNullOrWhiteSpace(command.Nombre))
        {
            errores.Add(new DetalleErrorValidacion(
                null,
                nameof(command.Nombre),
                "ROL_NOMBRE_REQUERIDO",
                "El nombre del rol es requerido."));
        }

        if (errores.Count > 0)
            throw new ValidacionNegocioSeguridadException(errores);
    }

    public static void ValidarUpdate(ActualizarRolCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errores = new List<DetalleErrorValidacion>();

        if (command.IdRol <= 0)
        {
            errores.Add(new DetalleErrorValidacion(
                null,
                nameof(command.IdRol),
                "ROL_ID_INVALIDO",
                "El identificador del rol no es valido."));
        }

        if (errores.Count > 0)
            throw new ValidacionNegocioSeguridadException(errores);
    }
}
