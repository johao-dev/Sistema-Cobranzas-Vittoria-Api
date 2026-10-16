using Cobranzas_Vittoria.Application.Common.Excepciones;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Actualizar;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Application.Common;

public sealed class PermisoValidator
{
    public static void ValidarUpdate(UpdatePermisoCommand command)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        var errores = new List<DetalleErrorValidacion>();

        if (command.IdPermiso <= 0)
        {
            errores.Add(new DetalleErrorValidacion(
                null,
                nameof(command.IdPermiso),
                "PERMISO_ID_INVALIDO",
                "El identificador del permiso no es valido."));
        }

        if (errores.Count > 0)
            throw new ValidacionNegocioSeguridadException(errores);
    }
}