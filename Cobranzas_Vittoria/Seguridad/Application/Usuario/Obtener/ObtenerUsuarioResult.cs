namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.Obtener;

public sealed record ObtenerUsuarioResult(
    int IdUsuario,
    string Nombres,
    string Apellidos,
    string Correo,
    string UsuarioLogin,
    bool Activo,
    DateTime? FechaCreacion,
    string? UsuarioCreacion);
