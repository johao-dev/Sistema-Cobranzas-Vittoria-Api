namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Crear;

public sealed class CreatePermisoValidator
{
    public static void ValidarCreate(CreatePermisoCommand command)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        if (string.IsNullOrWhiteSpace(command.Codigo) || command.Codigo.Contains(' '))
            throw new ArgumentException("El codigo es requerido y no puede contener espacios.", nameof(command.Codigo));

        if (string.IsNullOrWhiteSpace(command.Nombre))
            throw new ArgumentException("El nombre es requerido.", nameof(command.Nombre));
    }
}