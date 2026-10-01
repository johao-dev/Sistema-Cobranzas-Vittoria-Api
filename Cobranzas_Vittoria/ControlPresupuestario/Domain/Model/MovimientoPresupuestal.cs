namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

/// <summary>Asiento del ledger presupuestal. Inmutable: solo se lee; las correcciones son nuevos asientos.</summary>
public sealed class MovimientoPresupuestal
{
    public long IdMovimientoPresupuestal { get; private set; }
    public int IdPresupuestoDetalle { get; private set; }
    public int IdTipoMovimientoPresupuestal { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public string? NombreTipo { get; private set; }
    public string? ClaveEvento { get; private set; }
    public string? Origen { get; private set; }
    public int IdOrigen { get; private set; }
    public string? Afectacion { get; private set; }
    public string? Direccion { get; private set; }
    public DateTime Fecha { get; private set; }
    public decimal Monto { get; private set; }
    public string? Observacion { get; private set; }
    public int IdPresupuesto { get; private set; }
    public int IdPresupuestoVersion { get; private set; }
    public int NumeroVersion { get; private set; }
    public string? EstadoPresupuesto { get; private set; }
    public int IdCatalogoPartida { get; private set; }
    public string? CodigoPartida { get; private set; }
    public string? NombrePartida { get; private set; }
    public string? CodigoPresupuesto { get; private set; }
    public int IdMoneda { get; private set; }
    public string? CodigoMoneda { get; private set; }
    public string? SimboloMoneda { get; private set; }

    /// <summary>False cuando la ClaveEvento ya existía (reintento idempotente).</summary>
    public bool EsNuevo { get; private set; }

    private MovimientoPresupuestal() { }

    public static MovimientoPresupuestal Reconstruir(long idMovimientoPresupuestal, int idPresupuestoDetalle,
        int idTipoMovimientoPresupuestal, string tipo, string? nombreTipo, string? claveEvento, string? origen, int idOrigen,
        string? afectacion, string? direccion, DateTime fecha, decimal monto, string? observacion, int idPresupuesto,
        int idPresupuestoVersion, int numeroVersion, string? estadoPresupuesto, int idCatalogoPartida,
        string? codigoPartida, string? nombrePartida, string? codigoPresupuesto, int idMoneda, string? codigoMoneda,
        string? simboloMoneda, bool esNuevo)
        => new()
        {
            IdMovimientoPresupuestal = idMovimientoPresupuestal,
            IdPresupuestoDetalle = idPresupuestoDetalle,
            IdTipoMovimientoPresupuestal = idTipoMovimientoPresupuestal,
            Tipo = tipo,
            NombreTipo = nombreTipo,
            ClaveEvento = claveEvento,
            Origen = origen,
            IdOrigen = idOrigen,
            Afectacion = afectacion,
            Direccion = direccion,
            Fecha = fecha,
            Monto = monto,
            Observacion = observacion,
            IdPresupuesto = idPresupuesto,
            IdPresupuestoVersion = idPresupuestoVersion,
            NumeroVersion = numeroVersion,
            EstadoPresupuesto = estadoPresupuesto,
            IdCatalogoPartida = idCatalogoPartida,
            CodigoPartida = codigoPartida,
            NombrePartida = nombrePartida,
            CodigoPresupuesto = codigoPresupuesto,
            IdMoneda = idMoneda,
            CodigoMoneda = codigoMoneda,
            SimboloMoneda = simboloMoneda,
            EsNuevo = esNuevo
        };
}
