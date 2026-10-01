namespace Cobranzas_Vittoria.Entities;

public class ProveedorTerreno
{
    public int IdProveedorTerreno { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }
}
