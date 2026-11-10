using Cobranzas_Vittoria.Notificaciones.Application;
using Cobranzas_Vittoria.Notificaciones.Application.Options;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarNotificacionesPendientes;
using Cobranzas_Vittoria.Notificaciones.Application.RecuperarNotificacionesInterrumpidas;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Application;

public class RecuperarNotificacionesInterrumpidasHandlerTests
{
    private NotificacionRepositoryStub _repository = null!;
    private RelojNotificaciones _reloj = null!;
    private RecuperarNotificacionesInterrumpidasHandler _handler = null!;

    [SetUp]
    public void Preparar()
    {
        _repository = new();
        _reloj = new();
        _handler = CrearHandler(new NotificacionProcesamientoOptions());
    }

    private RecuperarNotificacionesInterrumpidasHandler CrearHandler(NotificacionProcesamientoOptions options)
    {
        return new(_repository, Options.Create(options), _reloj,
            NullLogger<RecuperarNotificacionesInterrumpidasHandler>.Instance);
    }

    [Test]
    public async Task SinProcesamientosVencidos_DevuelveLoteVacio()
    {
        ResultadoProcesamientoLote resultado = await _handler.HandleAsync();

        Assert.That(resultado, Is.EqualTo(new ResultadoProcesamientoLote(0, 0, 0)));
        Assert.That(_repository.LimiteRecuperacion, Is.EqualTo(DatosNotificaciones.AHORA.AddMinutes(-10)));
        Assert.That(_repository.LimiteRecuperacion!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [TestCase(9)]
    [TestCase(10)]
    public async Task ProcesamientoQueNoSuperoElTimeout_NoSeModifica(int minutos)
    {
        Notificacion original = DatosNotificaciones.Procesando(inicioUtc: DatosNotificaciones.AHORA.AddMinutes(-minutos));
        _repository.Almacenadas[1] = original;

        ResultadoProcesamientoLote resultado = await _handler.HandleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(resultado.Guardadas, Is.Zero);
            Assert.That(_repository.Resultados, Is.Empty);
            Assert.That(_repository.Almacenadas[1], Is.SameAs(original));
            Assert.That(original.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Procesando));
        });
    }

    [TestCase(1, Notificacion.EstadoNotificacion.Pendiente)]
    [TestCase(2, Notificacion.EstadoNotificacion.Pendiente)]
    [TestCase(3, Notificacion.EstadoNotificacion.Error)]
    [TestCase(4, Notificacion.EstadoNotificacion.Error)]
    public async Task ProcesamientoVencido_AplicaPoliticaSinIncrementarIntentos(int intentos, Notificacion.EstadoNotificacion estadoEsperado)
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Procesando(intentos);

        ResultadoProcesamientoLote resultado = await _handler.HandleAsync();

        Notificacion guardada = _repository.Almacenadas[1];
        Assert.Multiple(() =>
        {
            Assert.That(resultado, Is.EqualTo(new ResultadoProcesamientoLote(1, 1, 0)));
            Assert.That(guardada.Estado, Is.EqualTo(estadoEsperado));
            Assert.That(guardada.Intentos, Is.EqualTo(intentos));
            Assert.That(_repository.Resultados.Single().IntentoEsperado, Is.EqualTo(intentos));
            Assert.That(guardada.FechaInicioProcesamiento, Is.Null);
            Assert.That(guardada.FechaEnvio, Is.Null);
            Assert.That(guardada.UltimoError, Does.Contain("no se confirmó"));
            Assert.That(guardada.FechaProximoIntento,
                Is.EqualTo(estadoEsperado == Notificacion.EstadoNotificacion.Pendiente ? DatosNotificaciones.AHORA.AddMinutes(5) : (DateTime?)null));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RecuperacionObsoleta_NoSobrescribeResultadoConfirmadoNiIntentoPosterior(bool intentoPosterior)
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Procesando();
        _repository.AntesDeGuardar = (_, _) =>
        {
            if (intentoPosterior)
            {
                _repository.Almacenadas[1] = DatosNotificaciones.Procesando(intentos: 2);
            }
            else
            {
                _repository.Almacenadas[1].MarcarComoEnviada(DatosNotificaciones.AHORA);
            }
        };

        ResultadoProcesamientoLote resultado = await _handler.HandleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(resultado, Is.EqualTo(new ResultadoProcesamientoLote(1, 0, 1)));
            Assert.That(_repository.Almacenadas[1].Estado,
                Is.EqualTo(intentoPosterior ? Notificacion.EstadoNotificacion.Procesando : Notificacion.EstadoNotificacion.Enviada));
            Assert.That(_repository.Almacenadas[1].Intentos, Is.EqualTo(intentoPosterior ? 2 : 1));
            Assert.That(_repository.Almacenadas[1].UltimoError, Is.Null);
        });
    }

    [Test]
    public async Task NotificacionRecuperada_VuelveAEnviarCuandoLlegaLaFechaDelReintento()
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Procesando();
        await _handler.HandleAsync();
        EmailSenderStub sender = new();
        ProcesarNotificacionesPendientesHandler pendientes = new(_repository, sender,
            Options.Create(new NotificacionProcesamientoOptions()), _reloj,
            NullLogger<ProcesarNotificacionesPendientesHandler>.Instance);

        await pendientes.HandleAsync();
        Assert.That(sender.Solicitudes, Is.Empty);

        _reloj.AhoraUtc = DatosNotificaciones.AHORA.AddMinutes(5);
        await pendientes.HandleAsync();

        Assert.That(sender.Solicitudes, Has.Count.EqualTo(1));
        Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Enviada));
        Assert.That(_repository.Almacenadas[1].Intentos, Is.EqualTo(2));
    }

    [Test]
    public async Task RespuestaTardiaDelProveedor_NoConfirmaUnIntentoQueYaFueRecuperado()
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Pendiente();
        TaskCompletionSource aceptacion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        EmailSenderStub sender = new() { AlEnviar = (_, _) => aceptacion.Task };
        ProcesarNotificacionesPendientesHandler pendientes = new(_repository, sender,
            Options.Create(new NotificacionProcesamientoOptions()), _reloj,
            NullLogger<ProcesarNotificacionesPendientesHandler>.Instance);

        Task<ResultadoProcesamientoLote> envioEnCurso = pendientes.HandleAsync();
        _reloj.AhoraUtc = DatosNotificaciones.AHORA.AddMinutes(11);
        await _handler.HandleAsync();

        aceptacion.SetResult();
        ResultadoProcesamientoLote resultadoEnvio = await envioEnCurso;

        Assert.Multiple(() =>
        {
            Assert.That(resultadoEnvio.ConflictosConcurrencia, Is.EqualTo(1));
            Assert.That(resultadoEnvio.Guardadas, Is.Zero);
            Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Pendiente));
            Assert.That(_repository.Almacenadas[1].FechaEnvio, Is.Null);
            Assert.That(_repository.Almacenadas[1].Intentos, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TimeoutYTamanioPersonalizados_SeAplicanConTokenDeCancelacion()
    {
        _handler = CrearHandler(new NotificacionProcesamientoOptions
        {
            TamanoLote = 1, MaximoIntentos = 1, TimeoutProcesamientoMinutos = 20, IntervaloReintentoMinutos = 8
        });
        _repository.Almacenadas[1] = DatosNotificaciones.Procesando(inicioUtc: DatosNotificaciones.AHORA.AddMinutes(-21));
        _repository.Almacenadas[2] = DatosNotificaciones.Copiar(DatosNotificaciones.Procesando(inicioUtc: DatosNotificaciones.AHORA.AddMinutes(-22)), 2);
        using CancellationTokenSource source = new();

        ResultadoProcesamientoLote resultado = await _handler.HandleAsync(source.Token);

        Assert.That(resultado.Guardadas, Is.EqualTo(1));
        Assert.That(_repository.LimiteRecuperacion, Is.EqualTo(DatosNotificaciones.AHORA.AddMinutes(-20)));
        Assert.That(_repository.CantidadSolicitada, Is.EqualTo(1));
        Assert.That(_repository.TokenRecibido, Is.EqualTo(source.Token));
        Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Error));
        Assert.That(_repository.Almacenadas[2].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Procesando));
    }

    [Test]
    public void FalloDePersistencia_SePropagaSinAfirmarRecuperacion()
    {
        _repository.Almacenadas[1] = DatosNotificaciones.Procesando();
        InvalidOperationException error = new("Persistencia indisponible");
        _repository.ErrorGuardado = error;

        Assert.That(Assert.ThrowsAsync<InvalidOperationException>(async () => await _handler.HandleAsync()), Is.SameAs(error));
        Assert.That(_repository.Almacenadas[1].Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Procesando));
    }

    [Test]
    public void SolicitudCancelada_NoConsultaProcesamientos()
    {
        using CancellationTokenSource source = new();
        source.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await _handler.HandleAsync(source.Token));
        Assert.That(_repository.LimiteRecuperacion, Is.Null);
    }
}
