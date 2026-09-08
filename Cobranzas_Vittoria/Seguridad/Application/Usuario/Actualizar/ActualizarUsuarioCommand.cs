namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.Actualizar;

public sealed record ActualizarUsuarioCommand(
    int IdUsuario,
    string? Nombres,
    string? Apellidos,
    string? Correo,
    string? UsuarioLogin,
    string? PasswordHash, // TODO: Quitar, ya que el cambio de contraseña será otro caso de uso independiente.
    bool? Activo);
