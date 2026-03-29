namespace Cobranzas_Vittoria.Dtos.GastosAdministrativos;

public class ProveedorGastoAdministrativoUpsertDto
{
    public int? IdProveedorGastoAdministrativo { get; set; }
    public int IdCategoriaGasto { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public string? Contacto { get; set; }
    public string? Telefono { get; set; }
    public string? Correo { get; set; }
    public bool Activo { get; set; } = true;
}
