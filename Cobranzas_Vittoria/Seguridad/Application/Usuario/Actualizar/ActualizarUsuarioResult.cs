namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.Actualizar;

public sealed record ActualizarUsuarioResult(
    int IdUsuario,
    string Nombres,
    string Apellidos,
    string Correo,
    string UsuarioLogin,
    bool Activo);
