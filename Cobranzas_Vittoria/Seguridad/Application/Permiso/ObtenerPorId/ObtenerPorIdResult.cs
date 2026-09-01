namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.ObtenerPorId;

public sealed record ObtenerPorIdResult(
    int IdPermiso,
    string Codigo,
    string Nombre,
    string Descripcion,
    bool Activo,
    DateTime? FechaCreacion,
    string? UsuarioCreacion);