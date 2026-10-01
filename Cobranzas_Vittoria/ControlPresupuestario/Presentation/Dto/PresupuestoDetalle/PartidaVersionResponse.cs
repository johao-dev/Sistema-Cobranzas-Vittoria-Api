using Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoDetalle;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.PresupuestoDetalle;

/// <summary>Partida incluida en una versión. Codigo y Nombre son los de la partida del catálogo.</summary>
public sealed record PartidaVersionResponse(int IdPresupuestoDetalle, int IdPresupuestoVersion, int IdCatalogoPartida,
    string? Codigo, string? Nombre, decimal MontoPresupuestado, string? Observacion, int? IdPartidaPadre, int Nivel,
    int IdTipoPartida, string? CodigoTipoPartida, string? NombreTipoPartida, bool PartidaActiva, bool EsHoja,
    DateTime? FechaCreacion, DateTime? FechaModificacion)
{
    public static PartidaVersionResponse Desde(PresupuestoDetalleResult r) => new(r.IdPresupuestoDetalle,
        r.IdPresupuestoVersion, r.IdCatalogoPartida, r.CodigoPartida, r.NombrePartida, r.MontoPresupuestado, r.Observacion,
        r.IdPartidaPadre, r.Nivel, r.IdTipoPartida, r.CodigoTipoPartida, r.NombreTipoPartida, r.PartidaActiva, r.EsHoja,
        r.FechaCreacion, r.FechaModificacion);
}

public sealed record CargaLoteResponse(int IdPresupuestoVersion, int Agregados, int Actualizados, int Eliminados,
    int PartidasEnVersion, decimal MontoTotal)
{
    public static CargaLoteResponse Desde(CargaLoteResult r)
        => new(r.IdPresupuestoVersion, r.Agregados, r.Actualizados, r.Eliminados, r.PartidasEnVersion, r.MontoTotal);
}
