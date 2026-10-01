namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto;

public sealed record CentroCostoResult(int IdCentroCosto, string Codigo, string Nombre, string? Descripcion,
    int IdTipoCentroCosto, string? CodigoTipoCentroCosto, string? NombreTipoCentroCosto, int? IdProyecto,
    string? NombreProyecto, bool Activo, DateTime? FechaCreacion, DateTime? FechaModificacion)
{
    public static CentroCostoResult Desde(Domain.Model.CentroCosto c) => new(c.IdCentroCosto, c.Codigo, c.Nombre,
        c.Descripcion, c.IdTipoCentroCosto, c.CodigoTipoCentroCosto, c.NombreTipoCentroCosto, c.IdProyecto,
        c.NombreProyecto, c.Activo, c.FechaCreacion, c.FechaModificacion);
}
