namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

/// <summary>Tipo de centro de costo. PROYECTO es el único que se asocia a un proyecto.</summary>
public sealed record TipoCentroCosto(int IdTipoCentroCosto, string Codigo, string Nombre, string? Descripcion, bool Activo)
{
    public const string Proyecto = "PROYECTO";
}
