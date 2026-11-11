using Cobranzas_Vittoria.Application.Common.Eventos;
using Cobranzas_Vittoria.Compras.Contracts.Eventos;
using Cobranzas_Vittoria.Infrastructure.Eventos;
using Cobranzas_Vittoria.Notificaciones.Application.Options;
using Cobranzas_Vittoria.Notificaciones.Application.ProcesarEvento;
using Cobranzas_Vittoria.Notificaciones.Application.RequerimientoCreado;
using Cobranzas_Vittoria.Notificaciones.Application.Services;
using Cobranzas_Vittoria.Notificaciones.Domain.Model;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cobranzas_Vittoria.Tests.Unit.Notificaciones.Application;

public class RequerimientoCreadoEventHandlerTests
{
    [Test]
    public async Task Entregar_MapeaContratoCompleto_InvocaCasoDeUsoUnaVezYPropagaToken()
    {
        ConsultaStub consulta = new();
        ConfiguracionRepositoryStub configuracion = new();
        NotificacionRepositoryStub notificaciones = new();
        PlantillaRendererStub renderer = new();
        LoggerNotificacionesStub<RequerimientoCreadoEventHandler> logger = new();
        RequerimientoCreadoEvent evento = Evento();
        RequerimientoCreadoEventHandler handler = Handler(consulta, configuracion, notificaciones, renderer, logger);
        using CancellationTokenSource cts = new();

        await handler.HandleAsync(evento, cts.Token);

        Notificacion registrada = notificaciones.Almacenadas.Values.Single();
        Assert.Multiple(() =>
        {
            Assert.That(configuracion.CodigoConsultado, Is.EqualTo("REQUERIMIENTO_CREADO"));
            Assert.That(configuracion.Consultas, Is.EqualTo(1));
            Assert.That(notificaciones.Registros, Is.EqualTo(1));
            Assert.That(registrada.IdEvento, Is.EqualTo(evento.IdEvento));
            Assert.That(registrada.IdReferencia, Is.EqualTo(evento.IdRequerimiento));
            Assert.That(registrada.Estado, Is.EqualTo(Notificacion.EstadoNotificacion.Pendiente));
            Assert.That(consulta.IdRecibido, Is.EqualTo(evento.IdRequerimiento));
            Assert.That(consulta.TokenRecibido, Is.EqualTo(cts.Token));
            Assert.That(notificaciones.TokenRecibido, Is.EqualTo(cts.Token));
            Assert.That(renderer.VariablesRecibidas, Is.EquivalentTo(new Dictionary<string, string?>
            {
                ["NumeroRequerimiento"] = "REQ-125", ["Proyecto"] = "Proyecto <Norte>",
                ["Solicitante"] = "Ana Pérez", ["Fecha"] = "10/11/2026",
                ["Url"] = "https://erp.example.com/sistema/requerimientos"
            }));
            Assert.That(logger.Mensajes.Single(), Does.Contain(evento.IdEvento.ToString()).And.Contain("Creada"));
        });
    }

    [Test]
    public async Task Publicar_MismaOcurrenciaDosVeces_ConservaIdYUnaSolaNotificacion()
    {
        ConfiguracionRepositoryStub configuracion = new();
        NotificacionRepositoryStub notificaciones = new();
        LoggerNotificacionesStub<RequerimientoCreadoEventHandler> logger = new();
        RequerimientoCreadoEventHandler handler = Handler(new ConsultaStub(), configuracion,
            notificaciones, new PlantillaRendererStub(), logger);
        using ServiceProvider services = new ServiceCollection().AddEventosEnMemoria()
            .AddScoped<IEventHandler<RequerimientoCreadoEvent>>(_ => handler).BuildServiceProvider();
        using IServiceScope scope = services.CreateScope();
        IEventPublisher publicador = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
        RequerimientoCreadoEvent evento = Evento();

        await publicador.PublishAsync(evento);
        await publicador.PublishAsync(evento);

        Assert.That(notificaciones.Almacenadas, Has.Count.EqualTo(1));
        Assert.That(notificaciones.Almacenadas.Values.Single().IdEvento, Is.EqualTo(evento.IdEvento));
        Assert.That(configuracion.Consultas, Is.EqualTo(2));
        Assert.That(logger.Mensajes.Last(), Does.Contain("YaRegistrada"));
    }

    [Test]
    public void Entregar_FallaGeneracion_PropagaAlEmisorSinReintentarNiDuplicarLogs()
    {
        ConfiguracionRepositoryStub configuracion = new();
        NotificacionRepositoryStub notificaciones = new() { ErrorRegistro = new TimeoutException() };
        LoggerNotificacionesStub<RequerimientoCreadoEventHandler> logger = new();
        RequerimientoCreadoEventHandler handler = Handler(new ConsultaStub(), configuracion,
            notificaciones, new PlantillaRendererStub(), logger);

        Assert.ThrowsAsync<TimeoutException>(() => handler.HandleAsync(Evento()));
        Assert.That(notificaciones.Registros, Is.EqualTo(1));
        Assert.That(logger.Mensajes, Is.Empty);
    }

    [Test]
    public void Entregar_Cancelado_NoConsultaNiGenera()
    {
        ConsultaStub consulta = new();
        NotificacionRepositoryStub notificaciones = new();
        RequerimientoCreadoEventHandler handler = Handler(consulta, new ConfiguracionRepositoryStub(),
            notificaciones, new PlantillaRendererStub(), new LoggerNotificacionesStub<RequerimientoCreadoEventHandler>());
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(() => handler.HandleAsync(Evento(), cts.Token));
        Assert.That(consulta.IdRecibido, Is.Zero);
        Assert.That(notificaciones.Registros, Is.Zero);
    }

    [TestCase("https://erp.example.com", "requerimientos", "https://erp.example.com/requerimientos")]
    [TestCase("https://erp.example.com/sistema/", "requerimientos", "https://erp.example.com/sistema/requerimientos")]
    [TestCase("http://localhost:4200/", "requerimientos", "http://localhost:4200/requerimientos")]
    public void Enlace_RespetaBaseConfiguradaYRutaReal(string urlBase, string ruta, string esperado)
    {
        NotificacionEnlacesOptions opciones = new() { UrlBaseFrontend = urlBase, RutaRequerimientos = ruta };
        Assert.That(opciones.ConstruirUrlRequerimientos(), Is.EqualTo(esperado));
    }

    [TestCase("", "requerimientos")]
    [TestCase("ftp://erp.example.com", "requerimientos")]
    [TestCase("https://usuario:secreto@erp.example.com", "requerimientos")]
    [TestCase("https://erp.example.com?url=otro", "requerimientos")]
    [TestCase("https://erp.example.com", "//otro.example.com")]
    public void Enlace_ConfiguracionInvalida_FallaSinIncluirValores(string urlBase, string ruta)
    {
        NotificacionEnlacesOptions opciones = new() { UrlBaseFrontend = urlBase, RutaRequerimientos = ruta };
        InvalidOperationException? error = Assert.Throws<InvalidOperationException>(() => opciones.ConstruirUrlRequerimientos());
        Assert.That(error!.Message, Does.Not.Contain("secreto"));
    }

    private static RequerimientoCreadoEvent Evento() => new(Guid.NewGuid(), 125, "REQ-125", new DateOnly(2026, 11, 10), DatosNotificaciones.AHORA);

    private static RequerimientoCreadoEventHandler Handler(ConsultaStub consulta, ConfiguracionRepositoryStub configuracion,
        NotificacionRepositoryStub notificaciones, PlantillaRendererStub renderer,
        LoggerNotificacionesStub<RequerimientoCreadoEventHandler> logger)
    {
        ProcesarEventoHandler procesar = new(configuracion, notificaciones,
            new DestinatarioResolver([new DestinatarioResolverStub()]), renderer, new RelojNotificaciones());
        return new RequerimientoCreadoEventHandler(consulta, procesar, Options.Create(new NotificacionEnlacesOptions
        {
            UrlBaseFrontend = "https://erp.example.com/sistema"
        }), logger);
    }

    private sealed class ConsultaStub : IConsultaRequerimientoCreado
    {
        public int IdRecibido { get; private set; }
        public CancellationToken TokenRecibido { get; private set; }
        public Task<DatosRequerimientoCreado> ObtenerAsync(int idRequerimiento, CancellationToken cancellationToken = default)
        {
            IdRecibido = idRequerimiento;
            TokenRecibido = cancellationToken;
            return Task.FromResult(new DatosRequerimientoCreado("Proyecto <Norte>", "Ana Pérez"));
        }
    }
}
