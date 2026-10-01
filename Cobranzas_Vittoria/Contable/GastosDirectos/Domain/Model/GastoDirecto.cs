namespace Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;

/// <summary>Gasto directo imputado a una partida de la versión aprobada, con su contexto presupuestal.</summary>
public sealed record GastoDirecto
{
    public int IdGastoDirecto { get; init; }
    public int IdPresupuestoDetalle { get; init; }
    public int? IdProveedor { get; init; }
    public string? Proveedor { get; init; }
    public int IdMoneda { get; init; }
    public string Moneda { get; init; } = string.Empty;
    public DateTime Fecha { get; init; }
    public string Concepto { get; init; } = string.Empty;
    public string? Descripcion { get; init; }
    public decimal Monto { get; init; }
    public string Estado { get; init; } = string.Empty;
    public DateTime FechaCreacion { get; init; }
    public DateTime? FechaActualizacion { get; init; }
    public int IdPresupuesto { get; init; }
    public string CodigoPresupuesto { get; init; } = string.Empty;
    public int IdCentroCosto { get; init; }
    public string CodigoCentroCosto { get; init; } = string.Empty;
    public string CentroCosto { get; init; } = string.Empty;
    public int? IdProyecto { get; init; }
    public int IdCatalogoPartida { get; init; }
    public string CodigoPartida { get; init; } = string.Empty;
    public string Partida { get; init; } = string.Empty;
    public int TotalDocumentos { get; init; }
    public string? CodigoSeccionGasto { get; init; }
    public string? NombreSeccionGasto { get; init; }
    public int? IdMonedaOriginal { get; init; }
    public string? MonedaOriginal { get; init; }
    public decimal? MontoOriginal { get; init; }
    public decimal? TipoCambio { get; init; }
    public DateTime? FechaTipoCambio { get; init; }
}

public sealed record GastoDirectoDocumento(int IdGastoDirectoDocumento, int IdGastoDirecto, string TipoDocumento,
    string NombreArchivo, string RutaArchivo, string? Extension, DateTime FechaCreacion);

/// <summary>Documento ya guardado en el almacén, pendiente de registrar en el gasto.</summary>
public sealed record DocumentoNuevo(string TipoDocumento, string NombreArchivo, string RutaArchivo, string? Extension);

/// <summary>Proveedor ofrecido en una sección; DeLaSeccion marca a los de sus categorías antiguas.</summary>
public sealed record ProveedorSeccion(int IdProveedor, string RazonSocial, string? Ruc, bool DeLaSeccion);

/// <summary>Centro de costo admitido por una sección de gasto.</summary>
public sealed record CentroCostoSeccion(int IdCentroCosto, string Codigo, string Nombre, int? IdProyecto,
    string? CodigoTipoCentroCosto);

/// <summary>Partida de una sección con su saldo en la versión aprobada del centro de costo.</summary>
public sealed record PartidaDisponibleGasto(int IdPresupuestoDetalle, int IdPresupuesto, string CodigoPresupuesto,
    int IdPresupuestoVersion, int NumeroVersion, int IdCatalogoPartida, string CodigoPartida, string NombrePartida,
    int IdMoneda, string? CodigoMoneda, string? SimboloMoneda, decimal MontoPresupuestado, decimal MontoComprometido,
    decimal MontoEjecutado, decimal SaldoDisponible);

public sealed record FiltroGastosDirectos(string? Estado, int? IdProveedor, int? IdCentroCosto, DateTime? Desde,
    DateTime? Hasta, string? Seccion);
