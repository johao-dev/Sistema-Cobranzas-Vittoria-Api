namespace Cobranzas_Vittoria.ControlPresupuestario.Application.CentroCosto.Actualizar;

/// <summary>Codigo e IdTipoCentroCosto viajan por contrato, pero no se editan: deben coincidir con los actuales.</summary>
public sealed record ActualizarCentroCostoCommand(int IdCentroCosto, string Nombre, bool Activo, string? Descripcion,
    int? IdProyecto, string? Codigo, int? IdTipoCentroCosto);
