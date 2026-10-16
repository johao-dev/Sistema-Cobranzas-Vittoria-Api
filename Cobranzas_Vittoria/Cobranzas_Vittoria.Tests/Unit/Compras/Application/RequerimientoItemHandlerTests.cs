using Cobranzas_Vittoria.Compras.Application.Requerimiento.Items;
using Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Actualizar;
using Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Agregar;
using Cobranzas_Vittoria.Compras.Application.Requerimiento.Items.Eliminar;
using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Cobranzas_Vittoria.Compras.Domain.Model;
using Cobranzas_Vittoria.Compras.Domain.Persistence;
using Cobranzas_Vittoria.Compras.Domain.ValueObject;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Unit.Compras.Application;

public class RequerimientoItemHandlerTests
{
    [Test]
    public async Task Agregar_DevuelveLaIdentidadPersistidaYPropagaCancellationToken()
    {
        var repo = new RepositorioStub();
        using var cts = new CancellationTokenSource();
        var result = await Agregar(repo).HandleAsync(new(7, 22, 1.25m, null, "Nuevo"), cts.Token);
        Assert.Multiple(() =>
        {
            Assert.That(result.IdRequerimientoDetalle, Is.EqualTo(1000));
            Assert.That(result.Cantidad, Is.EqualTo(1.25m));
            Assert.That(repo.Lecturas, Is.EqualTo(1));
            Assert.That(repo.Escrituras, Is.EqualTo(1));
            Assert.That(repo.UltimoToken, Is.EqualTo(cts.Token));
        });
    }

    [Test]
    public async Task Actualizar_SoloCantidad_ConservaLosCamposOmitidos()
    {
        var repo = new RepositorioStub();
        var result = await Actualizar(repo).HandleAsync(new(7, 101, 2.75m, null, null, CamposItemRequerimiento.Cantidad));
        Assert.That((result.IdMaterial, result.IdPresupuestoDetalle, result.Cantidad, result.Observacion),
            Is.EqualTo((20, 44, 2.75m, "Original")));
        Assert.That(repo.Campos, Is.EqualTo(CamposItemRequerimiento.Cantidad));
    }

    [Test]
    public async Task Actualizar_ConNullExplicito_LimpiaPartidaYObservacion()
    {
        var repo = new RepositorioStub();
        var result = await Actualizar(repo).HandleAsync(new(7, 101, null, null, null,
            CamposItemRequerimiento.Partida | CamposItemRequerimiento.Observacion));
        Assert.That(result.IdPresupuestoDetalle, Is.Null);
        Assert.That(result.Observacion, Is.Null);
        Assert.That(result.Cantidad, Is.EqualTo(1m));
    }

    [TestCase(CamposItemRequerimiento.Ninguno)]
    [TestCase((CamposItemRequerimiento)8)]
    [TestCase(CamposItemRequerimiento.Cantidad)]
    public void Actualizar_PatchVacioDesconocidoOCantidadNull_NoConsultaNiEscribe(CamposItemRequerimiento campos)
    {
        var repo = new RepositorioStub();
        Assert.ThrowsAsync<ValidacionComprasException>(() =>
            Actualizar(repo).HandleAsync(new(7, 101, null, null, null, campos)));
        Assert.That((repo.Lecturas, repo.Escrituras), Is.EqualTo((0, 0)));
    }

    [Test]
    public async Task Eliminar_ConDetallePropio_DelegaSuIdentidadYPropagaCancellationToken()
    {
        var repo = new RepositorioStub();
        using var cts = new CancellationTokenSource();
        await Eliminar(repo).HandleAsync(new(7, 101), cts.Token);
        Assert.That(repo.DetalleEliminado, Is.EqualTo(101));
        Assert.That(repo.UltimoToken, Is.EqualTo(cts.Token));
        Assert.That(repo.Existente!.Items.Single().IdRequerimientoDetalle, Is.EqualTo(102));
    }

    [TestCase("agregar")]
    [TestCase("actualizar")]
    [TestCase("eliminar")]
    public void Operacion_ConIdInvalido_NoConsultaPersistencia(string operacion)
    {
        var repo = new RepositorioStub();
        Assert.ThrowsAsync<ValidacionComprasException>(() => Ejecutar(operacion, repo, 0));
        Assert.That((repo.Lecturas, repo.Escrituras), Is.EqualTo((0, 0)));
    }

    [TestCase("agregar")]
    [TestCase("actualizar")]
    [TestCase("eliminar")]
    public void Operacion_RequerimientoAusente_NoEscribe(string operacion)
    {
        var repo = new RepositorioStub { Existente = null };
        Assert.ThrowsAsync<RequerimientoNoEncontradoException>(() => Ejecutar(operacion, repo));
        Assert.That(repo.Escrituras, Is.Zero);
    }

    [TestCase("agregar")]
    [TestCase("actualizar")]
    [TestCase("eliminar")]
    public void Operacion_FueraDeAlmacen_NoEscribe(string operacion)
    {
        var repo = new RepositorioStub { Existente = CrearRequerimiento(EstadoRequerimiento.Registrado) };
        Assert.ThrowsAsync<ConflictoComprasException>(() => Ejecutar(operacion, repo));
        Assert.That(repo.Escrituras, Is.Zero);
    }

    [TestCase("actualizar")]
    [TestCase("eliminar")]
    public void Operacion_DetalleAjeno_NoEscribe(string operacion)
    {
        var repo = new RepositorioStub();
        Assert.ThrowsAsync<ItemRequerimientoNoEncontradoException>(() => Ejecutar(operacion, repo, idDetalle: 999));
        Assert.That(repo.Escrituras, Is.Zero);
    }

    [Test]
    public void Agregar_MaterialDuplicado_NoEscribe()
    {
        var repo = new RepositorioStub();
        Assert.ThrowsAsync<ConflictoComprasException>(() => Agregar(repo).HandleAsync(new(7, 20, 1m, null, null)));
        Assert.That(repo.Escrituras, Is.Zero);
    }

    [Test]
    public void Agregar_CantidadNoRepresentable_NoEscribe()
    {
        var repo = new RepositorioStub();
        Assert.ThrowsAsync<ValidacionComprasException>(() => Agregar(repo).HandleAsync(new(7, 22, 0.001m, null, null)));
        Assert.That(repo.Escrituras, Is.Zero);
    }

    [Test]
    public void Eliminar_UltimoItem_NoEscribe()
    {
        var repo = new RepositorioStub { Existente = CrearRequerimiento(unSoloItem: true) };
        Assert.ThrowsAsync<ConflictoComprasException>(() => Eliminar(repo).HandleAsync(new(7, 101)));
        Assert.That(repo.Escrituras, Is.Zero);
    }

    [TestCase("agregar")]
    [TestCase("actualizar")]
    [TestCase("eliminar")]
    public void Operacion_ConflictoAlPersistir_SePropaga(string operacion)
    {
        var repo = new RepositorioStub { ErrorEscritura = new ConflictoComprasException("ESTADO_CAMBIO", "El estado cambió.") };
        var error = Assert.ThrowsAsync<ConflictoComprasException>(() => Ejecutar(operacion, repo));
        Assert.That(error!.CodigoError, Is.EqualTo("ESTADO_CAMBIO"));
    }

    [TestCase("agregar")]
    [TestCase("actualizar")]
    [TestCase("eliminar")]
    public void Operacion_Cancelada_NoEscribe(string operacion)
    {
        var repo = new RepositorioStub();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAsync<OperationCanceledException>(() => Ejecutar(operacion, repo, ct: cts.Token));
        Assert.That(repo.Escrituras, Is.Zero);
    }

    private static Task Ejecutar(string operacion, RepositorioStub repo, int id = 7, int idDetalle = 101,
        CancellationToken ct = default) => operacion switch
    {
        "agregar" => Agregar(repo).HandleAsync(new(id, 22, 1m, null, null), ct),
        "actualizar" => Actualizar(repo).HandleAsync(new(id, idDetalle, 2m, null, null, CamposItemRequerimiento.Cantidad), ct),
        _ => Eliminar(repo).HandleAsync(new(id, idDetalle), ct)
    };

    private static AgregarItemHandler Agregar(RepositorioStub repo) => new(repo, NullLogger<AgregarItemHandler>.Instance);
    private static ActualizarItemHandler Actualizar(RepositorioStub repo) => new(repo, NullLogger<ActualizarItemHandler>.Instance);
    private static EliminarItemHandler Eliminar(RepositorioStub repo) => new(repo, NullLogger<EliminarItemHandler>.Instance);

    private static Requerimiento CrearRequerimiento(string estado = EstadoRequerimiento.EnviadoAlmacen, bool unSoloItem = false)
        => Requerimiento.Reconstruir(7, "REQ-001", new DateOnly(2026, 10, 15), 3, null, null, 5, estado, null,
            DateTime.UtcNow, unSoloItem
                ? [ItemRequerimiento.Reconstruir(101, 7, 20, 44, 1m, "Original")]
                : [ItemRequerimiento.Reconstruir(101, 7, 20, 44, 1m, "Original"),
                    ItemRequerimiento.Reconstruir(102, 7, 21, null, 2m, null)]);

    private sealed class RepositorioStub : IRequerimientoRepository
    {
        public Requerimiento? Existente { get; init; } = CrearRequerimiento();
        public int Lecturas { get; private set; }
        public int Escrituras { get; private set; }
        public int DetalleEliminado { get; private set; }
        public CamposItemRequerimiento Campos { get; private set; }
        public CancellationToken UltimoToken { get; private set; }
        public Exception? ErrorEscritura { get; init; }

        public Task<Requerimiento?> ObtenerConItemsAsync(int idRequerimiento, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Lecturas++;
            UltimoToken = cancellationToken;
            return Task.FromResult(Existente);
        }

        public Task<ItemRequerimiento> AgregarItemAsync(int idRequerimiento, ItemRequerimiento item,
            CancellationToken cancellationToken = default)
        {
            RegistrarEscritura(cancellationToken);
            return Task.FromResult(ItemRequerimiento.Reconstruir(1000, idRequerimiento, item.IdMaterial,
                item.IdPresupuestoDetalle, item.Cantidad, item.Observacion));
        }

        public Task<ItemRequerimiento> ActualizarItemAsync(int idRequerimiento, ItemRequerimiento item,
            CamposItemRequerimiento campos, CancellationToken cancellationToken = default)
        {
            RegistrarEscritura(cancellationToken);
            Campos = campos;
            return Task.FromResult(item);
        }

        public Task EliminarItemAsync(int idRequerimiento, int idDetalle, CancellationToken cancellationToken = default)
        {
            RegistrarEscritura(cancellationToken);
            DetalleEliminado = idDetalle;
            return Task.CompletedTask;
        }

        private void RegistrarEscritura(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Escrituras++;
            UltimoToken = ct;
            if (ErrorEscritura is not null) throw ErrorEscritura;
        }
    }
}
