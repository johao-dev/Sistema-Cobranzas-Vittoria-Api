namespace Cobranzas_Vittoria.Contable.GastosDirectos.Application.Listar;

public sealed record ListarGastosDirectosQuery(string? Estado, int? IdProveedor, int? IdCentroCosto, DateTime? Desde,
    DateTime? Hasta, string? Seccion);
