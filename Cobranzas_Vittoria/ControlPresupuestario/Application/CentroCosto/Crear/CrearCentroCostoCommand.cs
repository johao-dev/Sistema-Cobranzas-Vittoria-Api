namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Crear;

public sealed record CrearCentroCostoCommand(string Codigo, string Nombre, int IdTipoCentroCosto, string? Descripcion,
    int? IdProyecto);
