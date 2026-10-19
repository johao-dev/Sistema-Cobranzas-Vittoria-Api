using Cobranzas_Vittoria.Seguridad.Application.Permiso.Actualizar;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Habilitar;
using Cobranzas_Vittoria.Seguridad.Application.Permiso.Deshabilitar;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Application.Common;

public sealed class PermisoValidator
{
    public static void ValidarUpdate(UpdatePermisoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        
        ValidarIdPermiso(command.IdPermiso);
    }

    public static void ValidarEnable(EnablePermisoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidarIdPermiso(command.IdPermiso);
    }

    public static void ValidarDisable(DisablePermisoCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidarIdPermiso(command.IdPermiso);
    }

    private static void ValidarIdPermiso(int idPermiso)
    {
        if (idPermiso <= 0)
        {
            throw new ValidacionNegocioSeguridadException(
                "IdPermiso",
                "PERMISO_ID_INVALIDO",
                "El identificador del permiso no es valido.");
        }
    }
}
