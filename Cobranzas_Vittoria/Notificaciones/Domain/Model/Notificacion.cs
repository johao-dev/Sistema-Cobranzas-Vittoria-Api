using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Validator;

namespace Cobranzas_Vittoria.Notificaciones.Domain.Model;

public class Notificacion
{
    public long IdNotificacion { get; private set; }
    public Guid IdEvento { get; private set; }
    public int IdCodigoEventoNotificacion { get; private set; }
    public int IdReferencia { get; private set; } // ID del registro que generó la notificación

    public string AsuntoGenerado { get; private set; } = string.Empty;
    public string CuerpoHtmlGenerado { get; private set; } = string.Empty;

    public EstadoNotificacion Estado { get; private set; }

    public DateTime FechaCreacion { get; private set; }
    public DateTime? FechaInicioProcesamiento { get; private set; }
    public DateTime? FechaProximoIntento { get; private set; }
    public DateTime? FechaEnvio { get; private set; }

    public int Intentos { get; private set; }
    public string? UltimoError { get; private set; }

    /// <summary>Instantánea de los destinatarios originales, independiente de cambios posteriores de configuración.</summary>
    public IReadOnlyCollection<NotificacionDestinatario> Destinatarios { get; private set; } = Array.Empty<NotificacionDestinatario>();

    public enum EstadoNotificacion
    {
        Pendiente,
        Procesando,
        Enviada,
        Error
    }

    private Notificacion() { }

    public static Notificacion Crear(
        Guid idEvento,
        int idCodigoEventoNotificacion,
        int idReferencia,
        string asuntoGenerado,
        string cuerpoHtmlGenerado,
        DateTime fechaCreacionUtc,
        IEnumerable<NotificacionDestinatario> destinatarios)
    {
        NotificacionValidator.ValidarCreacion(
            idEvento,
            idCodigoEventoNotificacion,
            idReferencia,
            asuntoGenerado,
            cuerpoHtmlGenerado);

        IReadOnlyCollection<NotificacionDestinatario> destinatariosValidados =
            NotificacionValidator.ValidarYCopiarDestinatarios(idCodigoEventoNotificacion, destinatarios);

        return new Notificacion
        {
            IdEvento = idEvento,
            IdCodigoEventoNotificacion = idCodigoEventoNotificacion,
            IdReferencia = idReferencia,
            AsuntoGenerado = asuntoGenerado,
            CuerpoHtmlGenerado = cuerpoHtmlGenerado,
            Estado = EstadoNotificacion.Pendiente,
            FechaCreacion = fechaCreacionUtc,
            Intentos = 0,
            Destinatarios = destinatariosValidados
        };
    }

    public bool PuedeProcesarse(DateTime ahoraUtc)
    {
        return Estado == EstadoNotificacion.Pendiente
            && (!FechaProximoIntento.HasValue ||
                FechaProximoIntento.Value <= ahoraUtc);
    }

    public void IniciarProcesamiento(DateTime ahoraUtc)
    {
        ValidarEstado(EstadoNotificacion.Pendiente, nameof(IniciarProcesamiento));

        if (FechaProximoIntento.HasValue && FechaProximoIntento.Value > ahoraUtc)
        {
            throw new ValidacionNegocioNotificacionException(
                "NOTIFICACION_REINTENTO_NO_DISPONIBLE",
                "La notificación aún no alcanzó la fecha programada para el próximo intento.");
        }

        Estado = EstadoNotificacion.Procesando;
        FechaInicioProcesamiento = ahoraUtc;
        FechaProximoIntento = null;
        Intentos++;
    }

    public void MarcarComoEnviada(DateTime fechaEnvioUtc)
    {
        ValidarEstado(EstadoNotificacion.Procesando, nameof(MarcarComoEnviada));

        Estado = EstadoNotificacion.Enviada;
        FechaEnvio = fechaEnvioUtc;
        FechaInicioProcesamiento = null;
        FechaProximoIntento = null;
        UltimoError = null;
    }

    public void RegistrarFallo(
        string error,
        DateTime ahoraUtc,
        int maxIntentos,
        TimeSpan intervaloReintento)
    {
        ValidarEstado(EstadoNotificacion.Procesando, nameof(RegistrarFallo));
        NotificacionValidator.ValidarFallo(error, maxIntentos, intervaloReintento);

        UltimoError = error;
        FechaInicioProcesamiento = null;

        if (Intentos >= maxIntentos)
        {
            Estado = EstadoNotificacion.Error;
            FechaProximoIntento = null;
            return;
        }

        Estado = EstadoNotificacion.Pendiente;
        FechaProximoIntento = ahoraUtc.Add(intervaloReintento);
    }

    private void ValidarEstado(EstadoNotificacion estadoEsperado, string operacion)
    {
        if (Estado != estadoEsperado)
        {
            throw new TransicionEstadoNotificacionInvalidaException(Estado, operacion);
        }
    }

    /// <summary>Restaura una notificación persistida sin normalizar datos ni ejecutar las reglas de creación.</summary>
    /// <param name="idNotificacion">Identificador persistido de la notificación.</param>
    /// <param name="idEvento">Identificador único de la ocurrencia del evento.</param>
    /// <param name="idCodigoEventoNotificacion">Identificador del código de evento asociado.</param>
    /// <param name="idReferencia">Identificador del registro de negocio relacionado.</param>
    /// <param name="asuntoGenerado">Asunto definitivo conservado al generar la notificación.</param>
    /// <param name="cuerpoHtmlGenerado">Contenido HTML definitivo conservado al generar la notificación.</param>
    /// <param name="estado">Estado de procesamiento persistido.</param>
    /// <param name="fechaCreacionUtc">Fecha de creación en UTC.</param>
    /// <param name="fechaInicioProcesamientoUtc">Inicio del intento actual en UTC, si corresponde.</param>
    /// <param name="fechaProximoIntentoUtc">Fecha programada para reintentar en UTC, si existe.</param>
    /// <param name="fechaEnvioUtc">Fecha del envío satisfactorio en UTC, si se realizó.</param>
    /// <param name="intentos">Cantidad de intentos de procesamiento iniciados.</param>
    /// <param name="ultimoError">Descripción persistida del último fallo, si existe.</param>
    /// <param name="destinatarios">Destinatarios persistidos; se copia la colección sin aplicar las reglas de alta.</param>
    /// <returns>La notificación con su identidad, contenido e información de procesamiento originales.</returns>
    public static Notificacion Reconstruir(
        long idNotificacion,
        Guid idEvento,
        int idCodigoEventoNotificacion,
        int idReferencia,
        string asuntoGenerado,
        string cuerpoHtmlGenerado,
        EstadoNotificacion estado,
        DateTime fechaCreacionUtc,
        DateTime? fechaInicioProcesamientoUtc,
        DateTime? fechaProximoIntentoUtc,
        DateTime? fechaEnvioUtc,
        int intentos,
        string? ultimoError,
        IEnumerable<NotificacionDestinatario> destinatarios)
    {
        ArgumentNullException.ThrowIfNull(destinatarios);
        IReadOnlyCollection<NotificacionDestinatario> destinatariosPersistidos = destinatarios.ToList().AsReadOnly();

        return new Notificacion
        {
            IdNotificacion = idNotificacion,
            IdEvento = idEvento,
            IdCodigoEventoNotificacion = idCodigoEventoNotificacion,
            IdReferencia = idReferencia,
            AsuntoGenerado = asuntoGenerado,
            CuerpoHtmlGenerado = cuerpoHtmlGenerado,
            Estado = estado,
            FechaCreacion = fechaCreacionUtc,
            FechaInicioProcesamiento = fechaInicioProcesamientoUtc,
            FechaProximoIntento = fechaProximoIntentoUtc,
            FechaEnvio = fechaEnvioUtc,
            Intentos = intentos,
            UltimoError = ultimoError,
            Destinatarios = destinatariosPersistidos
        };
    }
}
