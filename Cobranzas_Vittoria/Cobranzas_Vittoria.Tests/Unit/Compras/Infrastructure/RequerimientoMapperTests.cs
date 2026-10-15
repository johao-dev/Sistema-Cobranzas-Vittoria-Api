using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Cobranzas_Vittoria.Compras.Domain.ValueObject;
using Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Entity;
using Cobranzas_Vittoria.Compras.Infrastructure.Persistence.Mapper;

namespace Cobranzas_Vittoria.Tests.Unit.Compras.Infrastructure;

public class RequerimientoMapperTests
{
    [TestCase(EstadoRequerimiento.Registrado)]
    [TestCase(EstadoRequerimiento.EnviadoAlmacen)]
    [TestCase(EstadoRequerimiento.ValidadoAlmacen)]
    [TestCase(EstadoRequerimiento.AprobadoCoordinador)]
    [TestCase(EstadoRequerimiento.Rechazado)]
    [TestCase(EstadoRequerimiento.EnviadoOC)]
    [TestCase(EstadoRequerimiento.GeneradoOC)]
    [TestCase(EstadoRequerimiento.Anulado)]
    public void IdaYVuelta_ConItems_ConservaTodasLasColumnasSinNormalizar(string estado)
    {
        var cabecera = Cabecera(estado);
        var detalle = Detalle();
        var otroDetalle = Detalle();
        otroDetalle.IdRequerimientoDetalle = 102;
        otroDetalle.IdMaterial = 21;
        otroDetalle.IdPresupuestoDetalle = 40;
        otroDetalle.Cantidad = 9999999999999999.99m;
        otroDetalle.Observacion = null;

        var domain = RequerimientoMapper.ToDomain(cabecera, [detalle, otroDetalle]);
        var filas = RequerimientoMapper.ToEntityConItems(domain);

        Assert.Multiple(() =>
        {
            Assert.That(filas.Requerimiento.IdRequerimiento, Is.EqualTo(cabecera.IdRequerimiento));
            Assert.That(filas.Requerimiento.NumeroRequerimiento, Is.EqualTo(cabecera.NumeroRequerimiento));
            Assert.That(filas.Requerimiento.FechaRequerimiento, Is.EqualTo(cabecera.FechaRequerimiento));
            Assert.That(filas.Requerimiento.IdProyecto, Is.EqualTo(cabecera.IdProyecto));
            Assert.That(filas.Requerimiento.Descripcion, Is.EqualTo(cabecera.Descripcion));
            Assert.That(filas.Requerimiento.FechaEntrega, Is.EqualTo(cabecera.FechaEntrega));
            Assert.That(filas.Requerimiento.IdUsuarioSolicitante, Is.EqualTo(cabecera.IdUsuarioSolicitante));
            Assert.That(filas.Requerimiento.Estado, Is.EqualTo(cabecera.Estado));
            Assert.That(filas.Requerimiento.Observacion, Is.EqualTo(cabecera.Observacion));
            Assert.That(filas.Requerimiento.FechaCreacion, Is.EqualTo(cabecera.FechaCreacion));
            Assert.That(filas.Requerimiento.FechaCreacion.Kind, Is.EqualTo(cabecera.FechaCreacion.Kind));
            Assert.That(filas.Items, Has.Count.EqualTo(2));
        });
        for (var i = 0; i < filas.Items.Count; i++)
        {
            var esperado = i == 0 ? detalle : otroDetalle;
            var actual = filas.Items[i];
            Assert.That((actual.IdRequerimientoDetalle, actual.IdRequerimiento, actual.IdMaterial,
                actual.IdPresupuestoDetalle, actual.Cantidad, actual.Observacion),
                Is.EqualTo((esperado.IdRequerimientoDetalle, esperado.IdRequerimiento, esperado.IdMaterial,
                    esperado.IdPresupuestoDetalle, esperado.Cantidad, esperado.Observacion)));
        }
    }

    [Test]
    public void ToDomain_ConOpcionalesNulos_ConservaLosNulos()
    {
        var cabecera = Cabecera();
        cabecera.Descripcion = null;
        cabecera.FechaEntrega = null;
        cabecera.Observacion = null;
        var domain = RequerimientoMapper.ToDomain(cabecera, [Detalle()]);
        var resultado = RequerimientoMapper.ToEntity(domain);
        Assert.That((resultado.Descripcion, resultado.FechaEntrega, resultado.Observacion),
            Is.EqualTo((cabecera.Descripcion, cabecera.FechaEntrega, cabecera.Observacion)));
        Assert.That(domain.Items.Single().IdPresupuestoDetalle, Is.Null);
    }

    [Test]
    public void ToDomain_ConDatosHistoricos_NoEjecutaReglasDeCreacion()
    {
        var cabecera = Cabecera();
        cabecera.FechaEntrega = cabecera.FechaRequerimiento.AddDays(-1);
        var detalle = Detalle();
        detalle.Cantidad = 0m;
        var domain = RequerimientoMapper.ToDomain(cabecera, [detalle]);
        Assert.That(domain.FechaEntrega, Is.EqualTo(cabecera.FechaEntrega));
        Assert.That(domain.Items.Single().Cantidad, Is.Zero);
    }

    [Test]
    public void ToDomain_ConHistoricoSinLineas_PermiteSuLectura()
    {
        var domain = RequerimientoMapper.ToDomain(Cabecera(EstadoRequerimiento.Anulado), []);
        Assert.That(domain.Items, Is.Empty);
        Assert.That(domain.Estado, Is.EqualTo(EstadoRequerimiento.Anulado));
    }

    [Test]
    public void ToDomain_ConLineasDeOtroPadre_RechazaElGrafoInconsistente()
    {
        var detalle = Detalle();
        detalle.IdRequerimiento = 8;
        var error = Assert.Throws<ConflictoComprasException>(() =>
            RequerimientoMapper.ToDomain(Cabecera(), [detalle]));
        Assert.That(error!.CodigoError, Is.EqualTo("REQUERIMIENTO_ITEM_NO_PERTENECE"));
    }

    [Test]
    public void ToDomain_ConIdentidadesRepetidas_NoDuplicaDetallesPorUnJoin()
    {
        var error = Assert.Throws<ConflictoComprasException>(() =>
            RequerimientoMapper.ToDomain(Cabecera(), [Detalle(), Detalle()]));
        Assert.That(error!.CodigoError, Is.EqualTo("REQUERIMIENTO_DETALLE_DUPLICADO"));
    }

    [Test]
    public void ToDomain_ConMaterialHistoricoDuplicado_LoConservaParaSuRevision()
    {
        var detalle = Detalle();
        detalle.IdRequerimientoDetalle = 102;
        var domain = RequerimientoMapper.ToDomain(Cabecera(), [Detalle(), detalle]);
        Assert.That(domain.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public void ToDomain_AlCambiarLasFilasOriginales_NoModificaElAgregado()
    {
        var cabecera = Cabecera();
        var detalle = Detalle();
        var domain = RequerimientoMapper.ToDomain(cabecera, [detalle]);
        cabecera.NumeroRequerimiento = "Otro";
        detalle.Cantidad = 100m;
        Assert.That(domain.NumeroRequerimiento, Is.EqualTo(" REQ-001 "));
        Assert.That(domain.Items.Single().Cantidad, Is.EqualTo(1.25m));
    }

    [Test]
    public void ToDomain_ConColeccionNull_ImpideOmitirLaCargaDeItems()
        => Assert.Throws<ArgumentNullException>(() => RequerimientoMapper.ToDomain(Cabecera(), null!));

    private static RequerimientoEntity Cabecera(string estado = EstadoRequerimiento.EnviadoAlmacen) => new()
    {
        IdRequerimiento = 7,
        NumeroRequerimiento = " REQ-001 ",
        FechaRequerimiento = new DateOnly(2026, 10, 15),
        IdProyecto = 3,
        Descripcion = " Obra ",
        FechaEntrega = new DateOnly(2026, 10, 17),
        IdUsuarioSolicitante = 5,
        Estado = estado,
        Observacion = " Cabecera ",
        FechaCreacion = new DateTime(2026, 10, 15, 12, 30, 0, DateTimeKind.Unspecified)
    };

    private static RequerimientoDetalleEntity Detalle() => new()
    {
        IdRequerimientoDetalle = 101,
        IdRequerimiento = 7,
        IdMaterial = 20,
        IdPresupuestoDetalle = null,
        Cantidad = 1.25m,
        Observacion = " Línea "
    };
}
