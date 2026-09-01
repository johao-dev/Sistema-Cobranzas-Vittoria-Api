using Cobranzas_Vittoria.Application.Common.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Crear;

public sealed class CreatePermisoValidator
{
    public static void ValidarCreate(CreatePermisoCommand command)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        var errores = new List<DetalleErrorValidacion>();

        if (string.IsNullOrWhiteSpace(command.Codigo))
        {
            errores.Add(new DetalleErrorValidacion(
                null,
                nameof(command.Codigo),
                "PERMISO_CODIGO_REQUERIDO",
                "El codigo del permiso es requerido."));
        }
        else if (command.Codigo.Contains(' '))
        {
            errores.Add(new DetalleErrorValidacion(
                null,
                nameof(command.Codigo),
                "PERMISO_CODIGO_ESPACIOS",
                "El codigo del permiso no puede contener espacios."));
        }

        if (string.IsNullOrWhiteSpace(command.Nombre))
        {
            errores.Add(new DetalleErrorValidacion(
                null,
                nameof(command.Nombre),
                "PERMISO_NOMBRE_REQUERIDO",
                "El nombre del permiso es requerido."));
        }

        if (errores.Count > 0)
            throw new ValidacionNegocioSeguridadException(errores);
    }
}