namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Crear;

public sealed class CreatePermisoValidator
{
    public static void ValidarCreate(CreatePermisoCommand command)
    {
        // TODO: Reemplazar por excepciones específicas
        if (string.IsNullOrWhiteSpace(command.Codigo))
        {
            throw new ArgumentException("El código no puede estar vacío ni contener espacios.");
        }

        if (string.IsNullOrWhiteSpace(command.Nombre))
        {
            throw new ArgumentException("El nombre no puede estar vacío ni contener espacios.");
        }
    }
}