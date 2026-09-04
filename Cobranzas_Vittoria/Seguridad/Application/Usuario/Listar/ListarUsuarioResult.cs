namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.Listar;

public sealed record ListarUsuarioResult(
    int IdUsuario,
    string Nombres,
    string Apellidos,
    string Correo,
    string UsuarioLogin,
    bool Activo,
    DateTime? FechaCreacion,
    string? UsuarioCreacion);
