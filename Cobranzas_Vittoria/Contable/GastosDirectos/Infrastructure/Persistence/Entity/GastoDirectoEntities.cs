namespace Cobranzas_Vittoria.Contable.GastosDirectos.Infrastructure.Persistence.Entity;

/// <summary>Fila de contable.usp_GastoDirecto_Listar / usp_GastoDirecto_Obtener.</summary>
public sealed class GastoDirectoEntity
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
    public string? CodigoSeccionGasto { get; set; }
    public string? NombreSeccionGasto { get; set; }
    public int? IdMonedaOriginal { get; set; }
    public string? MonedaOriginal { get; set; }
    public decimal? MontoOriginal { get; set; }
    public decimal? TipoCambio { get; set; }
    public DateTime? FechaTipoCambio { get; set; }
}

public sealed class GastoDirectoDocumentoEntity
{
    public int IdGastoDirectoDocumento { get; set; }
    public int IdGastoDirecto { get; set; }
    public string TipoDocumento { get; set; } = string.Empty;
    public string NombreArchivo { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public string? Extension { get; set; }
    public DateTime FechaCreacion { get; set; }
}

public sealed class ProveedorSeccionEntity
{
    public int IdProveedor { get; set; }
    public string RazonSocial { get; set; } = string.Empty;
    public string? Ruc { get; set; }
    public bool DeLaSeccion { get; set; }
}

public sealed class CentroCostoSeccionEntity
{
    public int IdCentroCosto { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public int? IdProyecto { get; set; }
    public string? CodigoTipoCentroCosto { get; set; }
}

public sealed class PartidaDisponibleGastoEntity
{
    public int IdPresupuestoDetalle { get; set; }
    public int IdPresupuesto { get; set; }
    public string CodigoPresupuesto { get; set; } = string.Empty;
    public int IdPresupuestoVersion { get; set; }
    public int NumeroVersion { get; set; }
    public int IdCatalogoPartida { get; set; }
    public string CodigoPartida { get; set; } = string.Empty;
    public string NombrePartida { get; set; } = string.Empty;
    public int IdMoneda { get; set; }
    public string? CodigoMoneda { get; set; }
    public string? SimboloMoneda { get; set; }
    public decimal MontoPresupuestado { get; set; }
    public decimal MontoComprometido { get; set; }
    public decimal MontoEjecutado { get; set; }
    public decimal SaldoDisponible { get; set; }
}
