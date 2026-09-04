namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.Crear;

public sealed record CreateUsuarioCommand(
    string Nombres,
    string Apellidos,
    string Correo,
    string UsuarioLogin,
    string PasswordHash);
