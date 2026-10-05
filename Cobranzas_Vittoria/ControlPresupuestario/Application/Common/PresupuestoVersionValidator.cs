using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;

namespace Cobranzas_Vittoria.ControlPresupuestario.Application.Common;

public static class PresupuestoVersionValidator
{
    public static void ValidarIds(int idPresupuesto, int idPresupuestoVersion)
    {
        Validacion.Id(idPresupuesto, "IdPresupuesto");
        Validacion.Id(idPresupuestoVersion, "IdPresupuestoVersion");
    }

    /// <summary>Anular exige un motivo: la versión descartada queda documentada.</summary>
    public static string ValidarMotivoAnulacion(string? motivo)
    {
        var limpio = motivo?.Trim();
        if (string.IsNullOrEmpty(limpio))
            throw new ValidacionPresupuestariaException("MOTIVO_REQUERIDO", "Indica el motivo de la anulación.");
        if (limpio.Length > 500) throw ValidacionPresupuestariaException.Longitud("Motivo", 500);
        return limpio;
    }
}
