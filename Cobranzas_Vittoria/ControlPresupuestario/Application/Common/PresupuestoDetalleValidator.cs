namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

public static class PresupuestoDetalleValidator
{
    public static void ValidarIds(int idPresupuesto, int idPresupuestoVersion, int idPresupuestoDetalle)
    {
        PresupuestoVersionValidator.ValidarIds(idPresupuesto, idPresupuestoVersion);
        Validacion.Id(idPresupuestoDetalle, "IdPresupuestoDetalle");
    }
}
