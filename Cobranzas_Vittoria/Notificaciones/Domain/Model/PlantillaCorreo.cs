using Cobranzas_Vittoria.Notificaciones.Domain.Validator;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Model;

/// <summary>Contenido configurable del correo de un evento; no modifica correos ya generados.</summary>
public class PlantillaCorreo
{
    public int IdPlantilla { get; private set; }
    public int IdCodigoEventoNotificacion { get; private set; }
    public string Asunto { get; private set; } = string.Empty;
    public string CuerpoHtml { get; private set; } = string.Empty;
    public bool Activo { get; private set; }

    private PlantillaCorreo() { }

    /// <summary>Crea una plantilla activa para un evento de negocio.</summary>
    public static PlantillaCorreo Crear(int idCodigoEventoNotificacion, string asunto, string cuerpoHtml)
    {
        PlantillaCorreoValidator.ValidarEvento(idCodigoEventoNotificacion);

        PlantillaCorreo plantilla = new() { IdCodigoEventoNotificacion = idCodigoEventoNotificacion, Activo = true };
        plantilla.ActualizarContenido(asunto, cuerpoHtml);

        return plantilla;
    }

    /// <summary>Valida todo el contenido antes de asignarlo, conservando el HTML sin recortarlo.</summary>
    public void ActualizarContenido(string asunto, string cuerpoHtml)
    {
        string asuntoNormalizado = asunto?.Trim() ?? string.Empty;
        PlantillaCorreoValidator.ValidarContenido(asuntoNormalizado, cuerpoHtml);

        Asunto = asuntoNormalizado;
        CuerpoHtml = cuerpoHtml;
    }

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;

    /// <summary>Indica si puede utilizarse para nuevos correos; el caso de uso debe comprobarlo.</summary>
    public bool PuedeUtilizarse() => Activo;

    /// <summary>Restaura la plantilla sin normalizar el contenido ni aplicar las reglas de creación.</summary>
    public static PlantillaCorreo Reconstruir(
        int idPlantilla, int idCodigoEventoNotificacion, string asunto, string cuerpoHtml, bool activo)
    {
        return new PlantillaCorreo
        {
            IdPlantilla = idPlantilla,
            IdCodigoEventoNotificacion = idCodigoEventoNotificacion,
            Asunto = asunto,
            CuerpoHtml = cuerpoHtml,
            Activo = activo
        };
    }
}
