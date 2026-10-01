namespace Cobranzas_Vittoria.ControlPresupuestario.Domain.ValueObject;

/// <summary>Tipo de asiento del ledger presupuestal.</summary>
public sealed record TipoMovimientoPresupuestal(int IdTipoMovimientoPresupuestal, string Codigo, string Nombre,
    string? Descripcion, bool Activo)
{
    public const string Compromiso = "COMPROMISO";
    public const string Liberacion = "LIBERACION";
    public const string Ejecucion = "EJECUCION";
    public const string Ajuste = "AJUSTE";
}
