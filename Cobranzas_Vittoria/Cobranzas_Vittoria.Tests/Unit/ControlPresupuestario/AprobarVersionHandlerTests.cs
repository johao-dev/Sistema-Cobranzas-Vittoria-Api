using Cobranzas_Vittoria.Application.Common.Eventos;
using Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion;
using Cobranzas_Vittoria.ControlPresupuestario.Application.PresupuestoVersion.Aprobar;
using Cobranzas_Vittoria.ControlPresupuestario.Contracts.Eventos;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.Tests.Unit.Notificaciones.Application;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;

namespace Cobranzas_Vittoria.Tests.Unit.ControlPresupuestario;

public class AprobarVersionHandlerTests
{
    [Test]
    public async Task Aprobar_PublicaResultadoSqlTrasConfirmarYAntesDeConsultarRespuesta()
    {
        VersionRepositoryStub repository = new();
        PublicadorStub publicador = new()
        {
            AlPublicar = _ =>
            {
                Assert.That(repository.Confirmada, Is.True);
                Assert.That(repository.Lecturas, Is.EqualTo(1));
            }
        };

        PresupuestoVersionResult resultado = await Handler(repository, publicador, new()).HandleAsync(new(10, 25));

        PresupuestoAprobadoEvent evento = publicador.Eventos.Single();
        Assert.Multiple(() =>
        {
            Assert.That(evento.IdEvento, Is.Not.EqualTo(Guid.Empty));
            Assert.That(evento.IdPresupuesto, Is.EqualTo(10));
            Assert.That(evento.IdPresupuestoVersion, Is.EqualTo(25));
            Assert.That(evento.NumeroVersion, Is.EqualTo(2));
            Assert.That(evento.FechaAprobacionUtc, Is.EqualTo(repository.Resultado.FechaAprobacionUtc));
            Assert.That(evento.FechaOcurrenciaUtc, Is.EqualTo(DatosNotificaciones.AHORA));
            Assert.That(publicador.TokenRecibido, Is.EqualTo(CancellationToken.None));
            Assert.That(repository.UsuarioRecibido, Is.EqualTo("test-user"));
            Assert.That(resultado.Estado, Is.EqualTo("APROBADO"));
        });
    }

    [Test]
    public void Aprobar_VersionDeOtroPresupuesto_NoApruebaNiPublica()
    {
        VersionRepositoryStub repository = new();
        PublicadorStub publicador = new();

        Assert.ThrowsAsync<VersionPresupuestoNoEncontradaException>(() => Handler(repository, publicador, new()).HandleAsync(new(99, 25)));
        Assert.That(repository.Confirmada, Is.False);
        Assert.That(publicador.Eventos, Is.Empty);
    }

    [Test]
    public void Aprobar_ErrorSql_NoPublica()
    {
        VersionRepositoryStub repository = new() { ErrorAprobacion = new InvalidOperationException() };
        PublicadorStub publicador = new();

        Assert.ThrowsAsync<InvalidOperationException>(() => Handler(repository, publicador, new()).HandleAsync(new(10, 25)));
        Assert.That(publicador.Eventos, Is.Empty);
    }

    [Test]
    public async Task Aprobar_ErrorDePublicacion_ConservaExitoYRegistraSinDatosSensibles()
    {
        VersionRepositoryStub repository = new();
        PublicadorStub publicador = new() { AlPublicar = _ => throw new TimeoutException("SQL y correo sensible") };
        LoggerNotificacionesStub<AprobarVersionHandler> logger = new();

        PresupuestoVersionResult resultado = await Handler(repository, publicador, logger).HandleAsync(new(10, 25));

        Assert.That(resultado.Estado, Is.EqualTo("APROBADO"));
        Assert.That(logger.Mensajes.Count(m => m.Contains("Falló la publicación", StringComparison.Ordinal)), Is.EqualTo(1));
        Assert.That(string.Join(" ", logger.Mensajes), Does.Contain("TimeoutException").And.Not.Contain("SQL y correo sensible"));
    }

    [Test]
    public void Aprobar_FallaConsultaFinal_ElEventoYaFuePublicado()
    {
        VersionRepositoryStub repository = new() { ErrorLecturaFinal = new TimeoutException() };
        PublicadorStub publicador = new();

        Assert.ThrowsAsync<TimeoutException>(() => Handler(repository, publicador, new()).HandleAsync(new(10, 25)));
        Assert.That(publicador.Eventos, Has.Count.EqualTo(1));
        Assert.That(repository.Confirmada, Is.True);
    }

    private static AprobarVersionHandler Handler(VersionRepositoryStub repository, PublicadorStub publicador,
        LoggerNotificacionesStub<AprobarVersionHandler> logger)
        => new(repository, new StubUsuarioActualService(), logger, publicador, new RelojNotificaciones());

    private sealed class PublicadorStub : IEventPublisher
    {
        public List<PresupuestoAprobadoEvent> Eventos { get; } = new();
        public Action<PresupuestoAprobadoEvent>? AlPublicar { get; init; }
        public CancellationToken TokenRecibido { get; private set; }

        public Task PublishAsync<TEvent>(TEvent evento, CancellationToken cancellationToken = default)
        {
            PresupuestoAprobadoEvent aprobado = (PresupuestoAprobadoEvent)(object)evento!;
            Eventos.Add(aprobado);
            TokenRecibido = cancellationToken;
            AlPublicar?.Invoke(aprobado);
            return Task.CompletedTask;
        }
    }

    private sealed class VersionRepositoryStub : IPresupuestoVersionRepository
    {
        public VersionAprobada Resultado { get; } = new(10, 25, 2, new DateTime(2026, 11, 14, 2, 0, 0, DateTimeKind.Utc));
        public Exception? ErrorAprobacion { get; init; }
        public Exception? ErrorLecturaFinal { get; init; }
        public bool Confirmada { get; private set; }
        public int Lecturas { get; private set; }
        public string? UsuarioRecibido { get; private set; }

        public Task<PresupuestoVersion?> ObtenerAsync(int idPresupuestoVersion)
        {
            Lecturas++;
            if (Lecturas > 1 && ErrorLecturaFinal is not null) throw ErrorLecturaFinal;

            PresupuestoVersion version = PresupuestoVersion.Reconstruir(25, 10, 2, 1,
                Confirmada ? "APROBADO" : "BORRADOR", null, null, null, null, null,
                Confirmada ? Resultado.FechaAprobacionUtc : null, null, null, null, null,
                "PRE-001", "Presupuesto", 1, 1, "PEN", "S/");
            return Task.FromResult<PresupuestoVersion?>(version);
        }

        public Task<VersionAprobada> AprobarAsync(int idPresupuestoVersion, string usuario)
        {
            if (ErrorAprobacion is not null) throw ErrorAprobacion;

            UsuarioRecibido = usuario;
            Confirmada = true;
            return Task.FromResult(Resultado);
        }

        public Task<IReadOnlyList<PresupuestoVersion>> ListarAsync(int idPresupuesto) => throw new NotSupportedException();
        public Task<VersionCreada> CrearNuevaAsync(int idPresupuesto, string? descripcion, string? motivoCambio, string usuario)
            => throw new NotSupportedException();
        public Task AnularAsync(int idPresupuestoVersion, string motivo, string usuario) => throw new NotSupportedException();
    }
}
