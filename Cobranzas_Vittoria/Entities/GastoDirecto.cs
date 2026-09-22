namespace Cobranzas_Vittoria.Entities;

public sealed class GastoDirecto
{
    public int IdGastoDirecto { get; set; }
    public int IdPresupuestoDetalle { get; set; }
    public int? IdProveedor { get; set; }
    public string? Proveedor { get; set; }
    public int IdMoneda { get; set; }
    public string Moneda { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string Concepto { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Monto { get; set; }
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaCreacion { get; set; }
    public DateTime? FechaActualizacion { get; set; }
    public int IdPresupuesto { get; set; }
    public string CodigoPresupuesto { get; set; } = string.Empty;
    public int IdCentroCosto { get; set; }
    public string CodigoCentroCosto { get; set; } = string.Empty;
    public string CentroCosto { get; set; } = string.Empty;
    public int? IdProyecto { get; set; }
    public int IdCatalogoPartida { get; set; }
    public string CodigoPartida { get; set; } = string.Empty;
    public string Partida { get; set; } = string.Empty;
    public int TotalDocumentos { get; set; }
}

public sealed class GastoDirectoDocumento
{
    public int IdGastoDirectoDocumento { get; set; }
    public int IdGastoDirecto { get; set; }
    public string TipoDocumento { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public DateTime FechaCreacion { get; set; }
}
