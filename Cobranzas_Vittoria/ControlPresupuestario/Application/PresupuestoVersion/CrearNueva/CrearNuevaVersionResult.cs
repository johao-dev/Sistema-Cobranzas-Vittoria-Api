namespace Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.CrearNueva;

public sealed record CrearNuevaVersionResult(int IdPresupuesto, int IdPresupuestoVersion, int NumeroVersion, string Estado,
    int? IdPresupuestoVersionBase, int CantidadDetallesCopiados);
