namespace Cobranzas_Vittoria.Seguridad.Application.Rol.Actualizar;

public sealed record ActualizarRolResult(
    int IdRol,
    string Nombre,
    string Descripcion,
    bool Activo,
    DateTime? FechaModificacion,
    string? UsuarioModificacion);
