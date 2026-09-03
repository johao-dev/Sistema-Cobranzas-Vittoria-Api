namespace Cobranzas_Vittoria.Seguridad.Infrastructure.Persistence.Entity;

public class RolEntity
{
    public int IdRol { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool Activo { get; set; }
    
    public DateTime? FechaCreacion { get; set; }
    public string? UsuarioCreacion { get; set; } = string.Empty;
    public DateTime? FechaModificacion { get; set; }
    public string? UsuarioModificacion { get; set; } = string.Empty;
}
