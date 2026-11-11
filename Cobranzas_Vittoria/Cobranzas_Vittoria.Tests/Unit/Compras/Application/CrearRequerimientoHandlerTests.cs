using Cobranzas_Vittoria.Compras.Application.Requerimiento.Crear;
using Cobranzas_Vittoria.Application.Common.Eventos;
using Cobranzas_Vittoria.Compras.Contracts.Eventos;
using Cobranzas_Vittoria.Tests.Unit.Notificaciones.Application;
using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Cobranzas_Vittoria.Compras.Domain.Model;
using Cobranzas_Vittoria.Compras.Domain.Persistence;
using Cobranzas_Vittoria.Compras.Domain.ValueObject;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Compras.Application;

public class CrearRequerimientoHandlerTests
{
    private static readonly DateOnly FECHA = new(2026, 11, 10);

    [Test]
    public async Task Crear_ConDefaults_UsaUsuarioConfiableYFechaLocalConservandoTextos()
    {
        PersistenciaDoble persistencia = new PersistenciaDoble();
        StubUsuarioActualService usuario = new StubUsuarioActualService { IdUsuario = 7 };
        CrearRequerimientoCommand command = Command() with
        {
            NumeroRequerimiento = "  MANUAL  ", FechaRequerimiento = default,
            Descripcion = "  Descripción  ", Observacion = "", FechaEntrega = FECHA.AddDays(-1)
        };
        using CancellationTokenSource cts = new CancellationTokenSource();

        CrearRequerimientoResult result = await Handler(persistencia, usuario).HandleAsync(command, cts.Token);

        Requerimiento creado = persistencia.Creaciones.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.IdRequerimiento, Is.EqualTo(123));
            Assert.That(creado.IdUsuarioSolicitante, Is.EqualTo(7));
            Assert.That(creado.FechaRequerimiento, Is.EqualTo(FECHA));
            Assert.That(creado.NumeroRequerimiento, Is.EqualTo("MANUAL"));
            Assert.That(creado.Descripcion, Is.EqualTo("  Descripción  "));
            Assert.That(creado.Observacion, Is.Empty);
            Assert.That(creado.Items.Single().Observacion, Is.EqualTo("  Ítem  "));
            Assert.That(creado.Items.Single().IdPresupuestoDetalle, Is.Null);
            Assert.That(creado.Estado, Is.EqualTo(EstadoRequerimiento.Registrado));
            Assert.That(persistencia.Tokens.All(t => t == cts.Token), Is.True);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("DUPLICADO")]
    public async Task Crear_SinNumeroDisponible_UsaElSiguienteGlobal(string? numero)
    {
        PersistenciaDoble persistencia = new PersistenciaDoble { Numeracion = new(true, 41) };
        await Handler(persistencia).HandleAsync(Command() with { NumeroRequerimiento = numero });
        Assert.That(persistencia.Creaciones.Single().NumeroRequerimiento, Is.EqualTo("42"));
    }

    [Test]
    public async Task Crear_ConColisionConfirmada_ConsultaOtraVezYReconstruyeElAgregado()
    {
        PersistenciaDoble persistencia = new PersistenciaDoble { Colisiones = 1 };
        await Handler(persistencia).HandleAsync(Command());

        Assert.That(persistencia.ConsultasNumeracion, Is.EqualTo(2));
        Assert.That(persistencia.Creaciones, Has.Count.EqualTo(2));
        Assert.That(persistencia.Creaciones[0], Is.Not.SameAs(persistencia.Creaciones[1]));
    }

    [Test]
    public void Crear_ConColisionesPersistentes_SeDetieneSinBucleInfinito()
    {
        PersistenciaDoble persistencia = new PersistenciaDoble { Colisiones = 10 };
        Assert.ThrowsAsync<NumeroRequerimientoOcupadoException>(() => Handler(persistencia).HandleAsync(Command()));
        Assert.That(persistencia.Creaciones, Has.Count.EqualTo(5));
    }

    [Test]
    public void Crear_ConTimeout_NoReintentaUnCommitDesconocido()
    {
        PersistenciaDoble persistencia = new PersistenciaDoble { Error = new TimeoutException() };
        Assert.ThrowsAsync<TimeoutException>(() => Handler(persistencia).HandleAsync(Command()));
        Assert.That(persistencia.Creaciones, Has.Count.EqualTo(1));
    }

    [Test]
    public void Crear_SinRangoNumerico_RechazaSinEscribir()
    {
        PersistenciaDoble persistencia = new PersistenciaDoble { Numeracion = new(true, int.MaxValue) };
        ConflictoComprasException? error = Assert.ThrowsAsync<ConflictoComprasException>(() => Handler(persistencia).HandleAsync(Command()));
        Assert.That(error!.CodigoError, Is.EqualTo("REQUERIMIENTO_NUMERACION_AGOTADA"));
        Assert.That(persistencia.Creaciones, Is.Empty);
    }

    [Test]
    public async Task Crear_NumeroManualDisponible_NoDependeDelRangoAutomatico()
    {
        PersistenciaDoble persistencia = new PersistenciaDoble { Numeracion = new(false, int.MaxValue) };
        await Handler(persistencia).HandleAsync(Command());
        Assert.That(persistencia.Creaciones.Single().NumeroRequerimiento, Is.EqualTo("MANUAL"));
    }

    [TestCase("sin-items")]
    [TestCase("item-null")]
    [TestCase("cantidad-cero")]
    [TestCase("precision")]
    [TestCase("material-duplicado")]
    public void Crear_ConEntradaInvalida_NoConsultaNiEscribe(string caso)
    {
        PersistenciaDoble persistencia = new PersistenciaDoble();
        CrearRequerimientoCommand command = Command();
        command = caso switch
        {
            "sin-items" => command with { Items = [] },
            "item-null" => command with { Items = [null] },
            "cantidad-cero" => command with { Items = [new(2, null, 0m, null)] },
            "precision" => command with { Items = [new(2, null, 0.001m, null)] },
            _ => command with { Items = [new(2, null, 1m, null), new(2, null, 2m, null)] }
        };

        Assert.That(async () => await Handler(persistencia).HandleAsync(command), Throws.InstanceOf<ComprasException>());
        Assert.That(persistencia.ConsultasContexto, Is.Zero);
        Assert.That(persistencia.Creaciones, Is.Empty);
    }

    [TestCase("proyecto")]
    [TestCase("solicitante")]
    [TestCase("material")]
    public void Crear_ConReferenciaAusente_NoEscribe(string caso)
    {
        PersistenciaDoble persistencia = new PersistenciaDoble();
        persistencia.Contexto = caso switch
        {
            "proyecto" => persistencia.Contexto with { ExisteProyecto = false },
            "solicitante" => persistencia.Contexto with { ExisteSolicitante = false },
            _ => persistencia.Contexto with { MaterialesExistentes = new HashSet<int>() }
        };
        Assert.ThrowsAsync<RecursoComprasNoEncontradoException>(() => Handler(persistencia).HandleAsync(Command()));
        Assert.That(persistencia.Creaciones, Is.Empty);
    }

    [TestCase("APROBADO", true, true, 3, true)]
    [TestCase("HISTORICO", true, true, 3, false)]
    [TestCase("BORRADOR", true, true, 3, false)]
    [TestCase("APROBADO", false, true, 3, false)]
    [TestCase("APROBADO", true, false, 3, false)]
    [TestCase("APROBADO", true, true, 4, false)]
    [TestCase("APROBADO", true, true, null, false)]
    public async Task Crear_ConPartida_ValidaEstadoActividadYProyecto(string estado, bool presupuestoActivo,
        bool centroActivo, int? proyecto, bool valida)
    {
        PersistenciaDoble persistencia = new PersistenciaDoble();
        persistencia.Contexto = persistencia.Contexto with
        {
            Partidas = [new PartidaCreacionRequerimiento
            {
                IdPresupuestoDetalle = 8, EstadoPresupuesto = estado, PresupuestoActivo = presupuestoActivo,
                CentroCostoActivo = centroActivo, IdProyecto = proyecto
            }]
        };
        CrearRequerimientoCommand command = Command() with { Items = [new(2, 8, 1m, null)] };

        if (valida)
        {
            await Handler(persistencia).HandleAsync(command);
            Assert.That(persistencia.Creaciones, Has.Count.EqualTo(1));
        }
        else
        {
            Assert.ThrowsAsync<ConflictoComprasException>(() => Handler(persistencia).HandleAsync(command));
            Assert.That(persistencia.Creaciones, Is.Empty);
        }
    }

    [Test]
    public void Crear_SinAutenticacion_NoConsultaPersistencia()
    {
        PersistenciaDoble persistencia = new PersistenciaDoble();
        StubUsuarioActualService usuario = new StubUsuarioActualService { EstaAutenticado = false };
        Assert.ThrowsAsync<AutenticacionException>(() => Handler(persistencia, usuario).HandleAsync(Command()));
        Assert.That(persistencia.ConsultasContexto, Is.Zero);
    }

    [Test]
    public void Crear_Cancelado_NoConsultaPersistencia()
    {
        PersistenciaDoble persistencia = new PersistenciaDoble();
        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(() => Handler(persistencia).HandleAsync(Command(), cts.Token));
        Assert.That(persistencia.ConsultasContexto, Is.Zero);
    }

    [Test]
    public async Task Crear_PublicaUnaVezDespuesDeConfirmar_ConDatosRealesYUtc()
    {
        PersistenciaDoble persistencia = new() { Colisiones = 1 };
        PublicadorDoble publicador = new()
        {
            AlPublicar = _ =>
            {
                Assert.That(persistencia.Confirmado, Is.True);
                return Task.CompletedTask;
            }
        };

        CrearRequerimientoResult resultado = await Handler(persistencia, publicador: publicador).HandleAsync(Command());

        RequerimientoCreadoEvent evento = publicador.Eventos.Single();
        Assert.Multiple(() =>
        {
            Assert.That(evento.IdEvento, Is.Not.EqualTo(Guid.Empty));
            Assert.That(evento.IdRequerimiento, Is.EqualTo(resultado.IdRequerimiento));
            Assert.That(evento.NumeroRequerimiento, Is.EqualTo("MANUAL"));
            Assert.That(evento.FechaRequerimiento, Is.EqualTo(FECHA));
            Assert.That(evento.FechaOcurrenciaUtc, Is.EqualTo(new DateTime(2026, 11, 11, 2, 0, 0, DateTimeKind.Utc)));
            Assert.That(evento.FechaOcurrenciaUtc.Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [Test]
    public void Crear_FalloAntesDelCommit_NoPublica()
    {
        PersistenciaDoble persistencia = new() { Error = new TimeoutException() };
        PublicadorDoble publicador = new();

        Assert.ThrowsAsync<TimeoutException>(() => Handler(persistencia, publicador: publicador).HandleAsync(Command()));
        Assert.That(publicador.Eventos, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Crear_FalloSecundarioInclusoCancelacion_ConservaExitoYRegistraUnaVez(bool cancelacion)
    {
        PersistenciaDoble persistencia = new();
        LoggerNotificacionesStub<CrearRequerimientoHandler> logger = new();
        PublicadorDoble publicador = new()
        {
            AlPublicar = _ => throw (cancelacion ? new OperationCanceledException("dato sensible") : new InvalidOperationException("dato sensible"))
        };
        CrearRequerimientoHandler handler = new(persistencia, persistencia, new StubUsuarioActualService(),
            new RelojFijo(), logger, publicador);

        CrearRequerimientoResult resultado = await handler.HandleAsync(Command());

        Assert.Multiple(() =>
        {
            Assert.That(resultado.IdRequerimiento, Is.EqualTo(123));
            Assert.That(persistencia.Confirmado, Is.True);
            Assert.That(persistencia.Creaciones, Has.Count.EqualTo(1));
            Assert.That(logger.Mensajes.Count(m => m.Contains("Falló la publicación")), Is.EqualTo(1));
            Assert.That(logger.Mensajes.Last(), Does.Contain(publicador.Eventos.Single().IdEvento.ToString()).And.Contain("123"));
            Assert.That(string.Join(" ", logger.Mensajes), Does.Not.Contain("dato sensible"));
        });
    }

    [Test]
    public async Task Crear_CancelacionHttpDespuesDelCommit_PublicaConTokenIndependiente()
    {
        using CancellationTokenSource cts = new();
        PersistenciaDoble persistencia = new() { DespuesDeConfirmar = cts.Cancel };
        PublicadorDoble publicador = new();

        CrearRequerimientoResult resultado = await Handler(persistencia, publicador: publicador).HandleAsync(Command(), cts.Token);

        Assert.Multiple(() =>
        {
            Assert.That(cts.IsCancellationRequested, Is.True);
            Assert.That(resultado.IdRequerimiento, Is.EqualTo(123));
            Assert.That(publicador.Eventos, Has.Count.EqualTo(1));
            Assert.That(publicador.TokenRecibido, Is.EqualTo(CancellationToken.None));
        });
    }

    [Test]
    public async Task Crear_EsperaLaPublicacionAntesDeRetornar()
    {
        TaskCompletionSource completada = new(TaskCreationOptions.RunContinuationsAsynchronously);
        PublicadorDoble publicador = new() { AlPublicar = _ => completada.Task };
        Task<CrearRequerimientoResult> creacion = Handler(new PersistenciaDoble(), publicador: publicador).HandleAsync(Command());

        try
        {
            Assert.That(publicador.Eventos, Has.Count.EqualTo(1));
            Assert.That(creacion.IsCompleted, Is.False);
        }
        finally
        {
            completada.SetResult();
            await creacion;
        }
    }

    private static CrearRequerimientoCommand Command() => new("MANUAL", FECHA, 3, null, null, null,
        [new CrearItemRequerimientoCommand(2, null, 1.25m, "  Ítem  ")]);

    private static CrearRequerimientoHandler Handler(PersistenciaDoble persistencia, StubUsuarioActualService? usuario = null,
        PublicadorDoble? publicador = null)
        => new(persistencia, persistencia, usuario ?? new StubUsuarioActualService(), new RelojFijo(),
            NullLogger<CrearRequerimientoHandler>.Instance, publicador ?? new PublicadorDoble());

    private sealed class PublicadorDoble : IEventPublisher
    {
        public List<RequerimientoCreadoEvent> Eventos { get; } = [];
        public CancellationToken TokenRecibido { get; private set; }
        public Func<RequerimientoCreadoEvent, Task>? AlPublicar { get; init; }

        public Task PublishAsync<TEvent>(TEvent evento, CancellationToken cancellationToken = default)
        {
            RequerimientoCreadoEvent creado = (RequerimientoCreadoEvent)(object)evento!;
            Eventos.Add(creado);
            TokenRecibido = cancellationToken;
            return AlPublicar?.Invoke(creado) ?? Task.CompletedTask;
        }
    }

    private sealed class RelojFijo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 11, 2, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("Prueba", TimeSpan.FromHours(-5), "Prueba", "Prueba");
    }

    private sealed class PersistenciaDoble : IRequerimientoRepository, IConsultaCreacionRequerimiento
    {
        public ContextoCreacionRequerimiento Contexto { get; set; } = new(true, true, new HashSet<int> { 2 }, []);
        public DisponibilidadNumeroRequerimiento Numeracion { get; init; } = new(false, 0);
        public int ConsultasContexto { get; private set; }
        public int ConsultasNumeracion { get; private set; }
        public int Colisiones { get; set; }
        public Exception? Error { get; init; }
        public bool Confirmado { get; private set; }
        public Action? DespuesDeConfirmar { get; init; }
        public List<Requerimiento> Creaciones { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];

        public Task<ContextoCreacionRequerimiento> ObtenerContextoAsync(int idProyecto, int idSolicitante,
            IReadOnlyCollection<int> materiales, IReadOnlyCollection<int> partidas, CancellationToken ct)
        {
            ConsultasContexto++;
            Tokens.Add(ct);
            return Task.FromResult(Contexto);
        }

        public Task<DisponibilidadNumeroRequerimiento> ObtenerNumeracionAsync(string numeroSolicitado, CancellationToken ct)
        {
            ConsultasNumeracion++;
            Tokens.Add(ct);
            return Task.FromResult(Numeracion);
        }

        public Task<int> CrearAsync(Requerimiento requerimiento, CancellationToken cancellationToken = default)
        {
            Creaciones.Add(requerimiento);
            Tokens.Add(cancellationToken);
            if (Colisiones-- > 0) throw new NumeroRequerimientoOcupadoException();
            if (Error is not null) throw Error;
            Confirmado = true;
            DespuesDeConfirmar?.Invoke();
            return Task.FromResult(123);
        }

        public Task<Requerimiento?> ObtenerConItemsAsync(int idRequerimiento, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ItemRequerimiento> AgregarItemAsync(int idRequerimiento, ItemRequerimiento item, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ItemRequerimiento> ActualizarItemAsync(int idRequerimiento, ItemRequerimiento item, CamposItemRequerimiento campos, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EliminarItemAsync(int idRequerimiento, int idDetalle, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
