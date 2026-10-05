using Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto;

namespace Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.CentroCosto;

public sealed record CentroCostoResponse(int IdCentroCosto, string Codigo, string Nombre, string? Descripcion,
    int IdTipoCentroCosto, string? CodigoTipoCentroCosto, string? NombreTipoCentroCosto, int? IdProyecto,
    string? NombreProyecto, bool Activo, DateTime? FechaCreacion, DateTime? FechaModificacion)
{
    public static CentroCostoResponse Desde(CentroCostoResult r) => new(r.IdCentroCosto, r.Codigo, r.Nombre, r.Descripcion,
        r.IdTipoCentroCosto, r.CodigoTipoCentroCosto, r.NombreTipoCentroCosto, r.IdProyecto, r.NombreProyecto, r.Activo,
        r.FechaCreacion, r.FechaModificacion);
}
