namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;

/// <summary>Partida incluida en una versión, con su monto presupuestado (en HTTP: "partida de la versión").</summary>
public sealed class PresupuestoDetalle
{
    public int IdPresupuestoDetalle { get; private set; }
    public int IdPresupuestoVersion { get; private set; }
    public int IdCatalogoPartida { get; private set; }
    public string? CodigoPartida { get; private set; }
    public string? NombrePartida { get; private set; }
    public int? IdPartidaPadre { get; private set; }
    public int Nivel { get; private set; }
    public int IdTipoPartida { get; private set; }
    public string? CodigoTipoPartida { get; private set; }
    public string? NombreTipoPartida { get; private set; }
    public bool PartidaActiva { get; private set; }
    public bool EsHoja { get; private set; }
    public decimal MontoPresupuestado { get; private set; }
    public string? Observacion { get; private set; }
    public DateTime? FechaCreacion { get; private set; }
    public DateTime? FechaModificacion { get; private set; }

    private PresupuestoDetalle() { }

    public static PresupuestoDetalle Crear(int idPresupuestoVersion, int idCatalogoPartida, decimal montoPresupuestado,
        string? observacion)
        => new()
        {
            IdPresupuestoVersion = Reglas.Id(idPresupuestoVersion, "IdPresupuestoVersion"),
            IdCatalogoPartida = Reglas.Id(idCatalogoPartida, "IdCatalogoPartida"),
            MontoPresupuestado = Reglas.Monto(montoPresupuestado, "El monto presupuestado"),
            Observacion = Reglas.Opcional(observacion, "Observacion", 500)
        };

    public static PresupuestoDetalle Reconstruir(int idPresupuestoDetalle, int idPresupuestoVersion, int idCatalogoPartida,
        string? codigoPartida, string? nombrePartida, int? idPartidaPadre, int nivel, int idTipoPartida,
        string? codigoTipoPartida, string? nombreTipoPartida, bool partidaActiva, bool esHoja, decimal montoPresupuestado,
        string? observacion, DateTime? fechaCreacion, DateTime? fechaModificacion)
        => new()
        {
            IdPresupuestoDetalle = idPresupuestoDetalle,
            IdPresupuestoVersion = idPresupuestoVersion,
            IdCatalogoPartida = idCatalogoPartida,
            CodigoPartida = codigoPartida,
            NombrePartida = nombrePartida,
            IdPartidaPadre = idPartidaPadre,
            Nivel = nivel,
            IdTipoPartida = idTipoPartida,
            CodigoTipoPartida = codigoTipoPartida,
            NombreTipoPartida = nombreTipoPartida,
            PartidaActiva = partidaActiva,
            EsHoja = esHoja,
            MontoPresupuestado = montoPresupuestado,
            Observacion = observacion,
            FechaCreacion = fechaCreacion,
            FechaModificacion = fechaModificacion
        };

    public void ActualizarMonto(decimal montoPresupuestado, string? observacion)
    {
        MontoPresupuestado = Reglas.Monto(montoPresupuestado, "El monto presupuestado");
        Observacion = Reglas.Opcional(observacion, "Observacion", 500);
    }
}
