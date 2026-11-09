namespace Cobranzas_Vittoria.Notificaciones.Domain.Model;

public class ReglaNotificacion
{
    public int IdReglaNotificacion { get; private set; }
    public int IdCodigoEventoNotificacion { get; private set; }
    public string Nombre { get; private set; } = null!;
    public bool Activo { get; private set; }

    private ReglaNotificacion() { }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;

    public bool PermiteNotificar() => Activo; // hm
}
