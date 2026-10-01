namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

public static class PresupuestoValidator
{
    public static void ValidarId(int idPresupuesto) => Validacion.Id(idPresupuesto, "IdPresupuesto");
}
