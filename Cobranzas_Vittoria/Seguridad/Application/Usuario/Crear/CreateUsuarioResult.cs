namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.Crear;

public sealed record CreateUsuarioResult(
    int IdUsuario,
    string Nombres,
    string Apellidos,
    string Correo,
    string UsuarioLogin,
    bool Activo,
    DateTime? FechaCreacion,
    string? UsuarioCreacion);
