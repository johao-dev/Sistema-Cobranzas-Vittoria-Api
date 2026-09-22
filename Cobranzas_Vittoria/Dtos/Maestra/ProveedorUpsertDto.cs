namespace Cobranzas_Vittoria.Dtos.Maestra;
public class ProveedorUpsertDto
{
    public int? IdProveedor { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public string? Direccion { get; set; }
    public string? Banco { get; set; }
    public string? CuentaCorriente { get; set; }
    public string? CCI { get; set; }
    public string? CuentaDetraccion { get; set; }
    public string? DescripcionServicio { get; set; }
    public string? Observacion { get; set; }
    public string? TrabajamosConProveedor { get; set; }
    public bool Activo { get; set; } = true;
}
