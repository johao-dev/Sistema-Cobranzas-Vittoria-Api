using Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Cobranzas_Vittoria.Notificaciones.Domain.Persistence;
using Microsoft.Extensions.Logging;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Application;

internal static class DatosNotificaciones
{
    internal static readonly DateTime AHORA = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);

    internal static ConfiguracionNotificacion Configuracion()
    {
        ReglaNotificacion regla = ReglaNotificacion.Crear(1, "Requerimiento creado");
        regla.AgregarDestinatario(ReglaDestinatario.Crear(15, TipoDestinatario.Rol, "2", NotificacionDestinatario.TipoEnvioConfig.To));
        regla.AgregarDestinatario(ReglaDestinatario.Crear(15, TipoDestinatario.Rol, "6", NotificacionDestinatario.TipoEnvioConfig.Cc));

        return new ConfiguracionNotificacion(1, "REQUERIMIENTO_CREADO", regla,
            PlantillaCorreo.Crear(1, "Nuevo {{NumeroRequerimiento}}", "<p>{{Proyecto}}</p><a href=\"{{Url}}\">Ver</a>"));
    }

    internal static ProcesarEventoCommand Command(Guid? idEvento = null)
    {
        return new ProcesarEventoCommand(idEvento ?? Guid.NewGuid(), "REQUERIMIENTO_CREADO", 125,
            new Dictionary<string, string?>
            {
                ["NumeroRequerimiento"] = "REQ-125",
                ["Proyecto"] = "Proyecto <Norte>",
                ["Solicitante"] = "Solicitante",
                ["Fecha"] = "10/11/2026",
                ["Url"] = "https://erp.example.com/requerimientos/125"
            });
    }

    internal static Notificacion Pendiente(int intentosPrevios = 0)
    {
        return Notificacion.Reconstruir(1, Guid.NewGuid(), 1, 125, "Asunto almacenado", "<p>HTML almacenado</p>",
            Notificacion.EstadoNotificacion.Pendiente, AHORA.AddDays(-1), null, null, null, intentosPrevios, null,
            [NotificacionDestinatario.Crear("almacen@example.com", "Almacén", NotificacionDestinatario.TipoEnvioConfig.To),
             NotificacionDestinatario.Crear("coordinador@example.com", null, NotificacionDestinatario.TipoEnvioConfig.Cc),
             NotificacionDestinatario.Crear("auditoria@example.com", "Auditoría", NotificacionDestinatario.TipoEnvioConfig.Bcc)]);
    }

    internal static Notificacion Procesando(int intentos = 1, DateTime? inicioUtc = null)
    {
        Notificacion notificacion = Pendiente(intentos - 1);
        notificacion.IniciarProcesamiento(inicioUtc ?? AHORA.AddMinutes(-11));
        return notificacion;
    }

    internal static Notificacion Copiar(Notificacion notificacion, long? id = null)
    {
        return Notificacion.Reconstruir(id ?? notificacion.IdNotificacion, notificacion.IdEvento,
            notificacion.IdCodigoEventoNotificacion, notificacion.IdReferencia, notificacion.AsuntoGenerado,
            notificacion.CuerpoHtmlGenerado, notificacion.Estado, notificacion.FechaCreacion,
            notificacion.FechaInicioProcesamiento, notificacion.FechaProximoIntento, notificacion.FechaEnvio,
            notificacion.Intentos, notificacion.UltimoError, notificacion.Destinatarios);
    }
}

internal sealed class RelojNotificaciones : TimeProvider
{
    internal DateTime AhoraUtc { get; set; } = DatosNotificaciones.AHORA;
    public override DateTimeOffset GetUtcNow() => new(AhoraUtc);
}

internal sealed class ConfiguracionRepositoryStub : IConfiguracionNotificacionRepository
{
    internal ConfiguracionNotificacion? Configuracion { get; set; } = DatosNotificaciones.Configuracion();
    internal string? CodigoConsultado { get; private set; }
    internal CancellationToken TokenRecibido { get; private set; }
    internal int Consultas { get; private set; }

    public Task<ConfiguracionNotificacion?> ObtenerPorCodigoEventoAsync(string codigoEvento, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Consultas++;
        CodigoConsultado = codigoEvento;
        TokenRecibido = cancellationToken;
        return Task.FromResult(Configuracion);
    }

    public Task GuardarReglaAsync(ReglaNotificacion regla, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task GuardarPlantillaAsync(PlantillaCorreo plantilla, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class NotificacionRepositoryStub : INotificacionRepository
{
    internal Dictionary<long, Notificacion> Almacenadas { get; } = new();
    internal List<(Notificacion Resultado, int IntentoEsperado)> Resultados { get; } = new();
    internal Action<Notificacion, int>? AntesDeGuardar { get; set; }
    internal Exception? ErrorRegistro { get; set; }
    internal Exception? ErrorGuardado { get; set; }
    internal int Registros { get; private set; }
    internal int Reclamaciones { get; private set; }
    internal int CantidadSolicitada { get; private set; }
    internal DateTime? AhoraReclamacion { get; private set; }
    internal DateTime? LimiteRecuperacion { get; private set; }
    internal CancellationToken TokenRecibido { get; private set; }

    public Task<ResultadoRegistroNotificacion> RegistrarSiNoExisteAsync(Notificacion notificacion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Registros++;
        TokenRecibido = cancellationToken;

        if (ErrorRegistro is not null)
        {
            throw ErrorRegistro;
        }

        Notificacion? existente = Almacenadas.Values.SingleOrDefault(actual => actual.IdEvento == notificacion.IdEvento);

        if (existente is not null)
        {
            return Task.FromResult(new ResultadoRegistroNotificacion(existente.IdNotificacion, false));
        }

        long id = Almacenadas.Count + 1;
        Almacenadas.Add(id, DatosNotificaciones.Copiar(notificacion, id));
        return Task.FromResult(new ResultadoRegistroNotificacion(id, true));
    }

    public Task<Notificacion?> ObtenerPorIdAsync(long idNotificacion, CancellationToken cancellationToken = default)
    {
        Notificacion? notificacion = Almacenadas.GetValueOrDefault(idNotificacion);
        return Task.FromResult(notificacion is null ? null : DatosNotificaciones.Copiar(notificacion));
    }

    public Task<IReadOnlyCollection<Notificacion>> ReclamarPendientesAsync(int cantidad, DateTime ahoraUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Reclamaciones++;
        CantidadSolicitada = cantidad;
        AhoraReclamacion = ahoraUtc;
        TokenRecibido = cancellationToken;

        List<Notificacion> pendientes = Almacenadas.Values
            .Where(actual => actual.Estado == Notificacion.EstadoNotificacion.Pendiente
                && (!actual.FechaProximoIntento.HasValue || actual.FechaProximoIntento <= ahoraUtc))
            .Take(cantidad).ToList();

        foreach (Notificacion notificacion in pendientes)
        {
            notificacion.IniciarProcesamiento(ahoraUtc);
        }

        return Task.FromResult<IReadOnlyCollection<Notificacion>>(pendientes.Select(actual => DatosNotificaciones.Copiar(actual)).ToList());
    }

    public Task<IReadOnlyCollection<Notificacion>> ObtenerProcesamientosVencidosAsync(int cantidad, DateTime fechaLimiteUtc, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CantidadSolicitada = cantidad;
        LimiteRecuperacion = fechaLimiteUtc;
        TokenRecibido = cancellationToken;

        IReadOnlyCollection<Notificacion> vencidas = Almacenadas.Values
            .Where(actual => actual.Estado == Notificacion.EstadoNotificacion.Procesando
                && actual.FechaInicioProcesamiento < fechaLimiteUtc)
            .Take(cantidad).Select(actual => DatosNotificaciones.Copiar(actual)).ToList();

        return Task.FromResult(vencidas);
    }

    public Task<bool> GuardarResultadoAsync(Notificacion notificacion, int intentoEsperado, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TokenRecibido = cancellationToken;
        Resultados.Add((DatosNotificaciones.Copiar(notificacion), intentoEsperado));

        if (ErrorGuardado is not null)
        {
            throw ErrorGuardado;
        }

        AntesDeGuardar?.Invoke(notificacion, intentoEsperado);
        Notificacion actual = Almacenadas[notificacion.IdNotificacion];

        if (actual.Estado != Notificacion.EstadoNotificacion.Procesando
            || actual.Intentos != intentoEsperado || notificacion.Intentos != intentoEsperado)
        {
            return Task.FromResult(false);
        }

        Almacenadas[actual.IdNotificacion] = DatosNotificaciones.Copiar(notificacion);
        return Task.FromResult(true);
    }
}

internal sealed class DestinatarioResolverStub : IDestinatarioResolver
{
    internal Func<ReglaDestinatario, IReadOnlyCollection<DireccionCorreo>> Resolver { get; set; }
        = _ => [new DireccionCorreo("usuario@example.com", "Usuario")];
    internal List<ReglaDestinatario> Instrucciones { get; } = new();
    internal CancellationToken TokenRecibido { get; private set; }

    public Task<IReadOnlyCollection<DireccionCorreo>> ResolverAsync(ReglaDestinatario instruccion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Instrucciones.Add(instruccion);
        TokenRecibido = cancellationToken;
        return Task.FromResult(Resolver(instruccion));
    }
}

internal sealed class PlantillaRendererStub : IPlantillaRenderer
{
    internal ContenidoCorreoRenderizado Contenido { get; set; } = new("Asunto definitivo", "<p>HTML definitivo</p>");
    internal Exception? Error { get; set; }
    internal int Llamadas { get; private set; }
    internal PlantillaCorreo? PlantillaRecibida { get; private set; }
    internal IReadOnlyDictionary<string, string?>? VariablesRecibidas { get; private set; }
    internal CancellationToken TokenRecibido { get; private set; }

    public Task<ContenidoCorreoRenderizado> RenderizarAsync(string codigoEvento, PlantillaCorreo plantilla,
        IReadOnlyDictionary<string, string?> variables, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Llamadas++;
        PlantillaRecibida = plantilla;
        VariablesRecibidas = variables;
        TokenRecibido = cancellationToken;

        if (Error is not null)
        {
            throw Error;
        }

        return Task.FromResult(Contenido);
    }
}

internal sealed class EmailSenderStub : IEmailSender
{
    internal List<SolicitudEnvioCorreo> Solicitudes { get; } = new();
    internal Func<SolicitudEnvioCorreo, CancellationToken, Task>? AlEnviar { get; set; }
    internal CancellationToken TokenRecibido { get; private set; }

    public Task EnviarAsync(SolicitudEnvioCorreo solicitud, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        TokenRecibido = cancellationToken;
        Solicitudes.Add(solicitud);
        return AlEnviar?.Invoke(solicitud, cancellationToken) ?? Task.CompletedTask;
    }
}

internal sealed class LoggerNotificacionesStub<T> : ILogger<T>
{
    internal List<string> Mensajes { get; } = new();
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Mensajes.Add(formatter(state, exception));

        if (exception is not null)
        {
            Mensajes.Add(exception.ToString());
        }
    }
}
