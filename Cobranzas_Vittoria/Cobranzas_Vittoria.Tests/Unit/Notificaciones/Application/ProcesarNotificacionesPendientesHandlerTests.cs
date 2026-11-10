using Cobranzas_Vittoria.Notificaciones.Application;
using Cobranzas_Vittoria.Notificaciones.Application.Options;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarNotificacionesPendientes;
using Cobranzas_Vittoria.Notificaciones.Domain.Exception;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Application;

public class ProcesarNotificacionesPendientesHandlerTests
{
    private NotificacionRepositoryStub _repository = null!;
    private EmailSenderStub _sender = null!;
    private LoggerNotificacionesStub<ProcesarNotificacionesPendientesHandler> _logger = null!;
    private RelojNotificaciones _reloj = null!;
    private ProcesarNotificacionesPendientesHandler _handler = null!;

    [SetUp]
    public void Preparar()
    {
        _repository = new();
        _sender = new();
        _logger = new();
        _reloj = new();
        _handler = CrearHandler(new NotificacionProcesamientoOptions());
    }

    private ProcesarNotificacionesPendientesHandler CrearHandler(NotificacionProcesamientoOptions options)
    {
        return new(_repository, _sender, Options.Create(options), _reloj, _logger);
    }

    [Test]
    public async Task LoteVacio_NoEnviaNiGuarda()
    {
        ResultadoProcesamientoLote resultado = await _handler.HandleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(resultado, Is.EqualTo(new ResultadoProcesamientoLote(0, 0, 0)));
            Assert.That(_sender.Solicitudes, Is.Empty);
            Assert.That(_repository.Resultados, Is.Empty);
            Assert.That(_repository.CantidadSolicitada, Is.EqualTo(3));
            Assert.That(_repository.AhoraReclamacion, Is.EqualTo(DatosNotificaciones.AHORA));
            Assert.That(_repository.AhoraReclamacion!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [Test]
    public async Task EnvioConfirmado_UsaInstantaneaYNoIncrementaDosVecesLosIntentos()
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente();
        _sender.AlEnviar = (_, _) =>
        {
            _reloj.AhoraUtc = DatosNotificaciones.AHORA.AddSeconds(2);
            return Task.CompletedTask;
        };

        ResultadoProcesamientoLote resultado = await _handler.HandleAsync();

        Notificacion guardada = _repository.Almacenadas[1];
        Assert.Multiple(() =>
        {
            Assert.That(resultado, Is.EqualTo(new ResultadoProcesamientoLote(1, 1, 0)));
            Assert.That(guardada.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Enviada));
            Assert.That(guardada.Intentos, Is.EqualTo(1));
            Assert.That(_repository.Resultados.Single().IntentoEsperado, Is.EqualTo(1));
            Assert.That(guardada.FechaEnvio, Is.EqualTo(DatosNotificaciones.AHORA.AddSeconds(2)));
            Assert.That(guardada.FechaInicioProcesamiento, Is.Null);
            Assert.That(_sender.Solicitudes.Single().Asunto, Is.EqualTo("Asunto almacenado"));
            Assert.That(_sender.Solicitudes.Single().CuerpoHtml, Is.EqualTo("<p>HTML almacenado</p>"));
            Assert.That(_sender.Solicitudes.Single().To.Single().Correo, Is.EqualTo("almacen@example.com"));
            Assert.That(_sender.Solicitudes.Single().To.Single().Nombre, Is.EqualTo("Almacén"));
            Assert.That(_sender.Solicitudes.Single().Cc.Single().Correo, Is.EqualTo("coordinador@example.com"));
            Assert.That(_sender.Solicitudes.Single().Bcc.Single().Correo, Is.EqualTo("auditoria@example.com"));
        });
    }

    [TestCase(true)]
    [TestCase(null)]
    public async Task FalloTransitorioODesconocido_ProgramaReintentoSinAfirmarEnvio(bool? esTransitorio)
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente();
        _sender.AlEnviar = (_, _) => throw new EnvioCorreoException("Causa del proveedor", esTransitorio);

        ResultadoProcesamientoLote resultado = await _handler.HandleAsync();

        Notificacion guardada = _repository.Almacenadas[1];
        Assert.Multiple(() =>
        {
            Assert.That(resultado.Guardadas, Is.EqualTo(1));
            Assert.That(guardada.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Pendiente));
            Assert.That(guardada.FechaProximoIntento, Is.EqualTo(DatosNotificaciones.AHORA.AddMinutes(5)));
            Assert.That(guardada.FechaEnvio, Is.Null);
            Assert.That(guardada.Intentos, Is.EqualTo(1));
        });
    }

    [TestCase(true)]
    [TestCase(null)]
    public async Task IntentosAgotados_TerminaEnError(bool? esTransitorio)
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente(intentosPrevios: 2);
        _sender.AlEnviar = (_, _) => throw new EnvioCorreoException("Fallo", esTransitorio);

        await _handler.HandleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Error));
            Assert.That(_repository.Almacenadas[1].Intentos, Is.EqualTo(3));
            Assert.That(_repository.Almacenadas[1].FechaProximoIntento, Is.Null);
        });
    }

    [Test]
    public async Task FalloPermanente_TerminaEnErrorSinSimularIntentosAgotados()
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente();
        _sender.AlEnviar = (_, _) => throw new EnvioCorreoException("Rechazo definitivo", false);

        await _handler.HandleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Error));
            Assert.That(_repository.Almacenadas[1].Intentos, Is.EqualTo(1));
            Assert.That(_repository.Almacenadas[1].FechaProximoIntento, Is.Null);
        });
    }

    [Test]
    public async Task FalloIndividualControlado_ContinuaConLaSiguienteNotificacion()
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente();
        _repository.Almacenadas[2] = DatosNotificaciones.Copiar(DatosNotificaciones.Pendiente(), 2);
        int llamadas = 0;
        _sender.AlEnviar = (_, _) => ++llamadas == 1
            ? Task.FromException(new EnvioCorreoException("No disponible", true))
            : Task.CompletedTask;

        ResultadoProcesamientoLote resultado = await _handler.HandleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(resultado, Is.EqualTo(new ResultadoProcesamientoLote(2, 2, 0)));
            Assert.That(_sender.Solicitudes, Has.Count.EqualTo(2));
            Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Pendiente));
            Assert.That(_repository.Almacenadas[2].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Enviada));
        });
    }

    [Test]
    public async Task ResultadoObsoleto_NoSobrescribeUnIntentoPosteriorYDevuelveConflicto()
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente();
        _repository.AntesDeGuardar = (_, _) => _repository.Almacenadas[1] = DatosNotificaciones.Procesando(intentos: 2);

        ResultadoProcesamientoLote resultado = await _handler.HandleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(resultado, Is.EqualTo(new ResultadoProcesamientoLote(1, 0, 1)));
            Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Procesando));
            Assert.That(_repository.Almacenadas[1].Intentos, Is.EqualTo(2));
            Assert.That(_repository.Almacenadas[1].FechaEnvio, Is.Null);
            Assert.That(_logger.Mensajes.Single(m => m.Contains("Conflicto", StringComparison.Ordinal)), Does.Contain("Conflicto"));
        });
    }

    [Test]
    public void ErrorDeProgramacionDelSender_SePropagaSinRegistrarFalloDeTransporte()
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente();
        _repository.Almacenadas[2] = DatosNotificaciones.Copiar(DatosNotificaciones.Pendiente(), 2);
        InvalidOperationException error = new("Configuración inválida");
        _sender.AlEnviar = (_, _) => throw error;

        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () => await _handler.HandleAsync()), Is.SameAs(error));
        Assert.Multiple(() =>
        {
            Assert.That(_repository.Resultados, Is.Empty);
            Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Procesando));
            Assert.That(_sender.Solicitudes, Has.Count.EqualTo(1));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ErrorDePersistencia_SePropagaInclusoSiTieneTipoDeTransporte(bool excepcionDeTransporte)
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente();
        Exception error = excepcionDeTransporte
            ? new EnvioCorreoException("Error mal clasificado en persistencia")
            : new InvalidOperationException("Persistencia indisponible");
        _repository.ErrorGuardado = error;

        Exception? recibido = Assert.CatchAsync<Exception>(async () => await _handler.HandleAsync());

        Assert.That(recibido, Is.SameAs(error));
        Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Procesando));
        Assert.That(_repository.Resultados, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DiagnosticoPersistidoYLogs_NoIncluyenMensajeNiCausaDelProveedor()
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente();
        _sender.AlEnviar = (_, _) => throw new EnvioCorreoException("CLAVE-SENSIBLE usuario@example.com contenido-completo", true,
            new Exception("credencial-interna"));

        await _handler.HandleAsync();

        string diagnostico = _repository.Almacenadas[1].UltimoError + string.Join(" ", _logger.Mensajes);
        Assert.Multiple(() =>
        {
            Assert.That(diagnostico, Does.Not.Contain("CLAVE-SENSIBLE"));
            Assert.That(diagnostico, Does.Not.Contain("usuario@example.com"));
            Assert.That(diagnostico, Does.Not.Contain("contenido-completo"));
            Assert.That(diagnostico, Does.Not.Contain("credencial-interna"));
            Assert.That(diagnostico, Does.Contain("transitorio"));
        });
    }

    [Test]
    public async Task AceptacionPendiente_NoMarcaNiGuardaAntesDeLaConfirmacion()
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente();
        TaskCompletionSource aceptacion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _sender.AlEnviar = (_, _) => aceptacion.Task;

        Task<ResultadoProcesamientoLote> procesamiento = _handler.HandleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(procesamiento.IsCompleted, Is.False);
            Assert.That(_repository.Resultados, Is.Empty);
            Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Procesando));
        });

        aceptacion.SetResult();
        await procesamiento;
        Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Enviada));
    }

    [Test]
    public void CancelacionDuranteEnvio_DejaElIntentoParaRecuperacionYNoSeClasificaComoFallo()
    {
        using CancellationTokenSource source = new();
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente();
        _sender.AlEnviar = (_, token) =>
        {
            source.Cancel();
            return Task.FromCanceled(token);
        };

        Assert.CatchAsync<OperationCanceledException>(async () => await _handler.HandleAsync(source.Token));
        Assert.That(_repository.Resultados, Is.Empty);
        Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Procesando));
    }

    [Test]
    public void SolicitudCancelada_NoReclamaTrabajo()
    {
        using CancellationTokenSource source = new();
        source.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await _handler.HandleAsync(source.Token));
        Assert.That(_repository.Reclamaciones, Is.Zero);
    }

    [Test]
    public async Task OpcionesPersonalizadas_ControlanLoteMaximoYEsperaSinMutacionPosterior()
    {
        NotificacionProcesamientoOptions options = new() { TamanoLote = 1, MaximoIntentos = 2, IntervaloReintentoMinutos = 7 };
        _handler = CrearHandler(options);
        options.MaximoIntentos = 99;
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente();
        _repository.Almacenadas[2] = DatosNotificaciones.Copiar(DatosNotificaciones.Pendiente(), 2);
        _sender.AlEnviar = (_, _) => throw new EnvioCorreoException("Fallo", true);
        using CancellationTokenSource source = new();

        await _handler.HandleAsync(source.Token);
        Assert.That(_repository.Almacenadas[1].FechaProximoIntento, Is.EqualTo(DatosNotificaciones.AHORA.AddMinutes(7)));
        Assert.That(_repository.CantidadSolicitada, Is.EqualTo(1));
        Assert.That(_sender.TokenRecibido, Is.EqualTo(source.Token));
        Assert.That(_repository.TokenRecibido, Is.EqualTo(source.Token));

        _reloj.AhoraUtc = DatosNotificaciones.AHORA.AddMinutes(7);
        await _handler.HandleAsync();
        Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Error));
        Assert.That(_repository.Almacenadas[1].Intentos, Is.EqualTo(2));
        Assert.That(_repository.Almacenadas[2].Intentos, Is.Zero);
    }
}
