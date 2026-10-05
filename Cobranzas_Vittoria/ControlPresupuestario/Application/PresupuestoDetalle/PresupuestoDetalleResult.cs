namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle;

/// <summary>Partida incluida en una versión de presupuesto.</summary>
public sealed record PresupuestoDetalleResult(int IdPresupuestoDetalle, int IdPresupuestoVersion, int IdCatalogoPartida,
    string? CodigoPartida, string? NombrePartida, int? IdPartidaPadre, int Nivel, int IdTipoPartida,
    string? CodigoTipoPartida, string? NombreTipoPartida, bool PartidaActiva, bool EsHoja, decimal MontoPresupuestado,
    string? Observacion, DateTime? FechaCreacion, DateTime? FechaModificacion)
{
    public static PresupuestoDetalleResult Desde(Domain.Model.PresupuestoDetalle d) => new(d.IdPresupuestoDetalle,
        d.IdPresupuestoVersion, d.IdCatalogoPartida, d.CodigoPartida, d.NombrePartida, d.IdPartidaPadre, d.Nivel,
        d.IdTipoPartida, d.CodigoTipoPartida, d.NombreTipoPartida, d.PartidaActiva, d.EsHoja, d.MontoPresupuestado,
        d.Observacion, d.FechaCreacion, d.FechaModificacion);
}

public sealed record CargaLoteResult(int IdPresupuestoVersion, int Agregados, int Actualizados, int Eliminados,
    int PartidasEnVersion, decimal MontoTotal)
{
    public static CargaLoteResult Desde(Domain.Model.ResultadoCargaLote r)
        => new(r.IdPresupuestoVersion, r.Agregados, r.Actualizados, r.Eliminados, r.PartidasEnVersion, r.MontoTotal);
}
