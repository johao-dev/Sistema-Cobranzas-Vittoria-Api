namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.Actualizar;

public sealed record ActualizarUsuarioCommand(
    int IdUsuario,
    string? Nombres,
    string? Apellidos,
    string? Correo,
    string? UsuarioLogin,
    string? PasswordHash,
    bool? Activo);
