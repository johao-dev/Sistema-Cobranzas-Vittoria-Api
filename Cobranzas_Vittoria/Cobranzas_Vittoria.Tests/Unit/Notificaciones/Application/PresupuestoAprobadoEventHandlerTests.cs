using Cobranzas_Vittoria.ControlPresupuestario.Contracts.Eventos;
using Cobranzas_Vittoria.Notificaciones.Application.PresupuestoAprobado;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Application;

public class PresupuestoAprobadoEventHandlerTests
{
    [TestCase("Proyecto <Norte>", "Proyecto <Norte>", "Ajuste <estructura>", "Ajuste <estructura>")]
    [TestCase(null, "Sin proyecto asociado", null, "No especificado")]
    public async Task Entregar_UsaVersionYFechaConfirmadas_ConvierteFechaALimaYPropagaToken(
        string? proyecto, string esperado, string? motivoCambio, string motivoEsperado)
    {
        ConsultaStub consulta = new() { Proyecto = proyecto, MotivoCambio = motivoCambio };
        ConfiguracionRepositoryStub configuracion = Configuracion();
        NotificacionRepositoryStub notificaciones = new();
        PlantillaRendererStub renderer = new();
        LoggerNotificacionesStub<PresupuestoAprobadoEventHandler> logger = new();
        PresupuestoAprobadoEvent evento = Evento();
        using CancellationTokenSource cts = new();

        await Handler(consulta, configuracion, notificaciones, renderer, logger).HandleAsync(evento, cts.Token);

        Notificacion registrada = notificaciones.Almacenadas.Values.Single();
        Assert.Multiple(() =>
        {
            Assert.That(configuracion.CodigoConsultado, Is.EqualTo("PRESUPUESTO_APROBADO"));
            Assert.That(registrada.IdEvento, Is.EqualTo(evento.IdEvento));
            Assert.That(registrada.IdReferencia, Is.EqualTo(evento.IdPresupuestoVersion));
            Assert.That(registrada.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Pendiente));
            Assert.That(consulta.IdRecibido, Is.EqualTo(evento.IdPresupuestoVersion));
            Assert.That(consulta.TokenRecibido, Is.EqualTo(cts.Token));
            Assert.That(notificaciones.TokenRecibido, Is.EqualTo(cts.Token));
            Assert.That(renderer.VariablesRecibidas, Is.EquivalentTo(new Dictionary<string, string?>
            {
                ["CodigoPresupuesto"] = "PRE-001", ["NombrePresupuesto"] = "Obra <Norte>",
                ["NumeroVersion"] = "2", ["Proyecto"] = esperado, ["Fecha"] = "13/11/2026",
                ["MotivoCambio"] = motivoEsperado
            }));
            Assert.That(logger.Mensajes.Single(), Does.Contain(evento.IdEvento.ToString()).And.Contain("Creada"));
        });
    }

    [Test]
    public async Task Entregar_MismaOcurrenciaDosVeces_RegistraUnaSolaNotificacion()
    {
        NotificacionRepositoryStub notificaciones = new();
        LoggerNotificacionesStub<PresupuestoAprobadoEventHandler> logger = new();
        PresupuestoAprobadoEventHandler handler = Handler(new ConsultaStub(), Configuracion(), notificaciones, new(), logger);
        PresupuestoAprobadoEvent evento = Evento();

        await handler.HandleAsync(evento);
        await handler.HandleAsync(evento);

        Assert.That(notificaciones.Almacenadas, Has.Count.EqualTo(1));
        Assert.That(logger.Mensajes.Last(), Does.Contain("YaRegistrada"));
    }

    [Test]
    public void Entregar_ErrorDePersistencia_PropagaSinDuplicarLogs()
    {
        NotificacionRepositoryStub notificaciones = new() { ErrorRegistro = new TimeoutException() };
        LoggerNotificacionesStub<PresupuestoAprobadoEventHandler> logger = new();
        PresupuestoAprobadoEventHandler handler = Handler(new(), Configuracion(), notificaciones, new(), logger);

        Assert.ThrowsAsync<TimeoutException>(() => handler.HandleAsync(Evento()));
        Assert.That(logger.Mensajes, Is.Empty);
    }

    [Test]
    public void Entregar_Cancelado_NoConsultaNiRegistra()
    {
        ConsultaStub consulta = new();
        NotificacionRepositoryStub notificaciones = new();
        PresupuestoAprobadoEventHandler handler = Handler(consulta, Configuracion(), notificaciones, new(), new());
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => handler.HandleAsync(Evento(), cts.Token));
        Assert.That(consulta.IdRecibido, Is.Zero);
        Assert.That(notificaciones.Registros, Is.Zero);
    }

    private static PresupuestoAprobadoEvent Evento() => new(Guid.NewGuid(), 10, 25, 2,
        new DateTime(2026, 11, 14, 2, 0, 0, DateTimeKind.Utc), DatosNotificaciones.AHORA);

    private static ConfiguracionRepositoryStub Configuracion()
    {
        ReglaNotificacion regla = ReglaNotificacion.Crear(8, "Presupuesto aprobado");
        regla.AgregarDestinatario(ReglaDestinatario.Crear(15, TipoDestinatario.Rol, "6", NotificacionDestinatario.TipoEnvioConfig.To));
        return new ConfiguracionRepositoryStub
        {
            Configuracion = new ConfiguracionNotificacion(8, "PRESUPUESTO_APROBADO", regla,
                PlantillaCorreo.Crear(8, "Presupuesto {{CodigoPresupuesto}}", "<p>{{Proyecto}}</p>"))
        };
    }

    private static PresupuestoAprobadoEventHandler Handler(ConsultaStub consulta, ConfiguracionRepositoryStub configuracion,
        NotificacionRepositoryStub notificaciones, PlantillaRendererStub renderer,
        LoggerNotificacionesStub<PresupuestoAprobadoEventHandler> logger)
    {
        ProcesarEventoHandler procesar = new(configuracion, notificaciones,
            new DestinatarioResolver([new DestinatarioResolverStub()]), renderer, new RelojNotificaciones());
        return new PresupuestoAprobadoEventHandler(consulta, procesar, logger);
    }

    private sealed class ConsultaStub : IConsultaPresupuestoAprobado
    {
        public string? Proyecto { get; init; } = "Proyecto <Norte>";
        public string? MotivoCambio { get; init; }
        public int IdRecibido { get; private set; }
        public CancellationToken TokenRecibido { get; private set; }

        public Task<DatosPresupuestoAprobado> ObtenerAsync(int idPresupuestoVersion, CancellationToken cancellationToken = default)
        {
            IdRecibido = idPresupuestoVersion;
            TokenRecibido = cancellationToken;
            return Task.FromResult(new DatosPresupuestoAprobado("PRE-001", "Obra <Norte>", Proyecto, MotivoCambio));
        }
    }
}
