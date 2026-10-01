namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Presupuesto.Crear;

/// <summary>El SP crea atómicamente el presupuesto y su versión 1 en BORRADOR.</summary>
public sealed record CrearPresupuestoResult(int IdPresupuesto, int IdPresupuestoVersion, int NumeroVersion, string Estado);
