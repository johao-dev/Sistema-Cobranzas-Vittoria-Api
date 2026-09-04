namespace Cobranzas_Vittoria.Seguridad.Presentation.Dto;

public sealed record ListarUsuarioResponse(
    IEnumerable<UsuarioResponse> Usuarios);
