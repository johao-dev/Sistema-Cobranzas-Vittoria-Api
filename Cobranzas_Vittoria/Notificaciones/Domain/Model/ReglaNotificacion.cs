using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Validator;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Model;

/// <summary>Controla la generación de notificaciones de un evento y sus instrucciones de destinatarios.</summary>
public class ReglaNotificacion
{
    private readonly List<ReglaDestinatario> _destinatarios = new();

    public int IdReglaNotificacion { get; private set; }
    public int IdCodigoEventoNotificacion { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public bool Activo { get; private set; }
    public IReadOnlyCollection<ReglaDestinatario> Destinatarios => _destinatarios.AsReadOnly();

    private ReglaNotificacion() { }

    /// <summary>Crea una regla activa que puede completarse agregando sus instrucciones de destinatarios.</summary>
    public static ReglaNotificacion Crear(int idCodigoEventoNotificacion, string nombre)
    {
        ReglaNotificacionValidator.ValidarEvento(idCodigoEventoNotificacion);
        string nombreNormalizado = ReglaNotificacionValidator.ValidarYNormalizarNombre(nombre);

        return new ReglaNotificacion
        {
            IdCodigoEventoNotificacion = idCodigoEventoNotificacion,
            Nombre = nombreNormalizado,
            Activo = true
        };
    }

    public void CambiarNombre(string nombre)
    {
        Nombre = ReglaNotificacionValidator.ValidarYNormalizarNombre(nombre);
    }

    /// <summary>Agrega una instrucción nueva sin duplicar estrategia, valor y participación.</summary>
    public void AgregarDestinatario(ReglaDestinatario destinatario)
    {
        ArgumentNullException.ThrowIfNull(destinatario);

        if (destinatario.IdReglaDestinatario != 0 || destinatario.IdReglaNotificacion != 0)
        {
            throw new ValidacionNegocioNotificacionException(
                "REGLA_DESTINATARIO_YA_PERSISTIDO", "Solo se pueden agregar instrucciones nuevas sin pertenencia persistida.");
        }

        if (_destinatarios.Any(actual => actual.TieneMismaConfiguracion(destinatario)))
        {
            throw new ValidacionNegocioNotificacionException(
                "REGLA_DESTINATARIO_DUPLICADO", "La regla ya contiene esa configuración de destinatario.");
        }

        _destinatarios.Add(destinatario);
    }

    /// <summary>Quita una instrucción de esta regla; devuelve false si la instancia no pertenece a ella.</summary>
    public bool QuitarDestinatario(ReglaDestinatario destinatario) => _destinatarios.Remove(destinatario);

    public void Activar() => Activo = true;

    public void Desactivar() => Activo = false;

    /// <summary>Indica si permite nuevas generaciones (RN-09), sin cancelar notificaciones existentes.</summary>
    public bool PermiteNotificar() => Activo;

    /// <summary>Restaura la regla y copia su colección persistida sin aplicar reglas de alta.</summary>
    public static ReglaNotificacion Reconstruir(
        int idReglaNotificacion, int idCodigoEventoNotificacion, string nombre, bool activo,
        IEnumerable<ReglaDestinatario> destinatarios)
    {
        ArgumentNullException.ThrowIfNull(destinatarios);

        ReglaNotificacion regla = new()
        {
            IdReglaNotificacion = idReglaNotificacion,
            IdCodigoEventoNotificacion = idCodigoEventoNotificacion,
            Nombre = nombre,
            Activo = activo
        };
        regla._destinatarios.AddRange(destinatarios);

        return regla;
    }
}
