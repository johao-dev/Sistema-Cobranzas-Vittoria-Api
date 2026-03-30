namespace Cobranzas_Vittoria.Dtos.Auth;

public class LoginResponseDto
{
    public int IdUsuario { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string? Apellidos { get; set; }
    public string? Correo { get; set; }
    public string UsuarioLogin { get; set; } = string.Empty;
    public string? NombreRol { get; set; }
}
