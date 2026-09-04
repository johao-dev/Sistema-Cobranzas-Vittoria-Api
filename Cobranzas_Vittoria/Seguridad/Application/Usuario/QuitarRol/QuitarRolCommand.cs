namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.QuitarRol;

public sealed record QuitarRolCommand(
    int IdUsuario,
    int IdRol);
