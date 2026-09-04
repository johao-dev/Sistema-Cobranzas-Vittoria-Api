namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;

public class UsuarioEntity
{
    public int IdUsuario { get; set; }
    public string Nombres { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string Correo { get; set; } = string.Empty;
    public string UsuarioLogin { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;

    public DateTime? FechaCreacion { get; set; }
    public string? UsuarioCreacion { get; set; } = string.Empty;
}
