using System.ComponentModel.DataAnnotations;

namespace Cobranzas_Vittoria.Notificaciones.Infrastructure.BackgroundJobs;

public sealed class NotificacionesWorkerOptions
{
    // La activación requiere una decisión explícita incluso en producción.
    public bool Habilitado { get; set; }

    [Range(1, 86400)]
    public int IntervaloSegundos { get; set; } = 30;
}
