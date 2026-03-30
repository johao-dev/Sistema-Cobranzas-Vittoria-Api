namespace Cobranzas_Vittoria.Entities;

public class GastoAdministrativoDocumento
{
    public int IdGastoAdministrativoDocumento { get; set; }
    public int IdGastoAdministrativo { get; set; }
    public string TipoDocumento { get; set; } = "Factura";
    public string NombreArchivo { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public DateTime FechaCreacion { get; set; }
}
