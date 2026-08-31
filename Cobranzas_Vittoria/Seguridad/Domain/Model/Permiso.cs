namespace Cobranzas_Vittoria.Seguridad.Domain.Model;

public class Permiso
{
    public int IdPermiso { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;

    public DateTime? FechaCreacion { get; set; }
    public string? UsuarioCreacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
    public string? UsuarioModificacion { get; set; }

    // TODO: Agregar métodos de negocio aquí...
}
