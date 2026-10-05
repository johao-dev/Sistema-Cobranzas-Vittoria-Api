namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Listar;

public sealed record ListarCentroCostoQuery(bool? Activo, int? IdTipoCentroCosto, int? IdProyecto, string? Busqueda);
