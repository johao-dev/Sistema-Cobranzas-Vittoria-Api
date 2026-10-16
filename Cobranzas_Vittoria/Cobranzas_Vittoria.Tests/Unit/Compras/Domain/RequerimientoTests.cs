using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Cobranzas_Vittoria.Compras.Domain.Model;
using Cobranzas_Vittoria.Compras.Domain.ValueObject;

namespace Cobranzas_Vittoria.Tests.Unit.Compras.Domain;

public class RequerimientoTests
{
    private static readonly DateOnly FECHA = new(2026, 10, 15);
    private static readonly DateTime INSTANTE = new(2026, 10, 15, 12, 30, 0, DateTimeKind.Utc);

    [Test]
    public void Crear_ConCabeceraYLineas_NaceRegistradoConIdentidadesPendientes()
    {
        var item = ItemRequerimiento.Crear(0, 20, null, 1.25m, "  Mortero  ");
        var requerimiento = Requerimiento.Crear("  REQ-001  ", FECHA, 3, 5, [item],
            "  Obra  ", FECHA.AddDays(2), "  Urgente  ", INSTANTE);

        Assert.Multiple(() =>
        {
            Assert.That(requerimiento.IdRequerimiento, Is.Zero);
            Assert.That(requerimiento.NumeroRequerimiento, Is.EqualTo("REQ-001"));
            Assert.That(requerimiento.FechaRequerimiento, Is.EqualTo(FECHA));
            Assert.That(requerimiento.IdProyecto, Is.EqualTo(3));
            Assert.That(requerimiento.IdUsuarioSolicitante, Is.EqualTo(5));
            Assert.That(requerimiento.Descripcion, Is.EqualTo("Obra"));
            Assert.That(requerimiento.FechaEntrega, Is.EqualTo(FECHA.AddDays(2)));
            Assert.That(requerimiento.Observacion, Is.EqualTo("Urgente"));
            Assert.That(requerimiento.FechaCreacion, Is.EqualTo(INSTANTE));
            Assert.That(requerimiento.Estado, Is.EqualTo(EstadoRequerimiento.Registrado));
            Assert.That(requerimiento.Items.Single().IdRequerimientoDetalle, Is.Zero);
            Assert.That(requerimiento.Items.Single().IdPresupuestoDetalle, Is.Null);
            Assert.That(requerimiento.Items.Single().Cantidad, Is.EqualTo(1.25m));
            Assert.That(requerimiento.Items.Single(), Is.Not.SameAs(item));
        });
    }

    [Test]
    public void Crear_SinItems_RechazaDocumentoVacio()
        => Assert.Throws<ValidacionComprasException>(() =>
            Requerimiento.Crear("REQ-001", FECHA, 3, 5, []));

    [Test]
    public void Crear_ConMaterialesDuplicados_RechazaLaSolicitud()
        => Assert.Throws<ConflictoComprasException>(() => Requerimiento.Crear("REQ-001", FECHA, 3, 5,
            [ItemRequerimiento.Crear(0, 20, null, 1m, null), ItemRequerimiento.Crear(0, 20, null, 2m, null)]));

    [Test]
    public void Crear_ConItemPersistido_NoLoApropiaDeOtroDocumento()
        => Assert.Throws<ConflictoComprasException>(() =>
            Requerimiento.Crear("REQ-001", FECHA, 3, 5, [Detalle()]));

    [TestCase(0, 5)]
    [TestCase(3, 0)]
    public void Crear_ConIdentificadoresInvalidos_Rechaza(int idProyecto, int idSolicitante)
        => Assert.Throws<ValidacionComprasException>(() => Requerimiento.Crear("REQ-001", FECHA,
            idProyecto, idSolicitante, [ItemRequerimiento.Crear(0, 20, null, 1m, null)]));

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("1234567890123456789012345678901")]
    public void Crear_ConNumeroInvalido_Rechaza(string numero)
        => Assert.Throws<ValidacionComprasException>(() => Requerimiento.Crear(numero, FECHA, 3, 5,
            [ItemRequerimiento.Crear(0, 20, null, 1m, null)]));

    [Test]
    public void Crear_ConEntregaAnteriorALaSolicitud_Rechaza()
        => Assert.Throws<ValidacionComprasException>(() => Requerimiento.Crear("REQ-001", FECHA, 3, 5,
            [ItemRequerimiento.Crear(0, 20, null, 1m, null)], fechaEntrega: FECHA.AddDays(-1)));

    [Test]
    public void Crear_ConFechaOmitida_Rechaza()
        => Assert.Throws<ValidacionComprasException>(() => Requerimiento.Crear("REQ-001", default, 3, 5,
            [ItemRequerimiento.Crear(0, 20, null, 1m, null)]));

    [TestCase("descripcion")]
    [TestCase("observacion")]
    public void Crear_ConTextoQueExcedeLaColumna_Rechaza(string campo)
        => Assert.Throws<ValidacionComprasException>(() => Requerimiento.Crear("REQ-001", FECHA, 3, 5,
            [ItemRequerimiento.Crear(0, 20, null, 1m, null)],
            descripcion: campo == "descripcion" ? new string('x', 251) : null,
            observacion: campo == "observacion" ? new string('x', 251) : null));

    [Test]
    public void AgregarItem_ConFraccionSinPartida_ConservaCabeceraYGeneraUnaLineaNueva()
    {
        var requerimiento = Reconstruido();
        var item = requerimiento.AgregarItem(21, 0.25m, null, "  Nuevo  ");

        Assert.Multiple(() =>
        {
            Assert.That(requerimiento.Items, Has.Count.EqualTo(2));
            Assert.That(requerimiento.Items.Last(), Is.SameAs(item));
            Assert.That(item.IdRequerimientoDetalle, Is.Zero);
            Assert.That(item.IdRequerimiento, Is.EqualTo(7));
            Assert.That(item.Cantidad, Is.EqualTo(0.25m));
            Assert.That(item.IdPresupuestoDetalle, Is.Null);
            Assert.That(item.Observacion, Is.EqualTo("Nuevo"));
            Assert.That(requerimiento.Estado, Is.EqualTo(EstadoRequerimiento.EnviadoAlmacen));
            Assert.That(requerimiento.Observacion, Is.EqualTo("Cabecera"));
        });
    }

    [Test]
    public void AgregarItem_ConMaterialExistente_NoModificaLaColeccion()
    {
        var requerimiento = Reconstruido();
        Assert.Throws<ConflictoComprasException>(() => requerimiento.AgregarItem(20, 1m, null, null));
        Assert.That(requerimiento.Items, Has.Count.EqualTo(1));
    }

    [TestCase("0", "REQUERIMIENTO_CANTIDAD_INVALIDA")]
    [TestCase("-1", "REQUERIMIENTO_CANTIDAD_INVALIDA")]
    [TestCase("0.001", "COMPRAS_PRECISION_INVALIDA")]
    [TestCase("10000000000000000", "COMPRAS_PRECISION_INVALIDA")]
    public void AgregarItem_ConCantidadInvalida_NoModificaLaColeccion(string cantidad, string codigo)
    {
        var requerimiento = Reconstruido();
        var error = Assert.Throws<ValidacionComprasException>(() => requerimiento.AgregarItem(21,
            decimal.Parse(cantidad, System.Globalization.CultureInfo.InvariantCulture), null, null));
        Assert.That(error!.CodigoError, Is.EqualTo(codigo));
        Assert.That(requerimiento.Items, Has.Count.EqualTo(1));
    }

    [TestCase(0, null)]
    [TestCase(21, 0)]
    [TestCase(21, -1)]
    public void AgregarItem_ConReferenciaInvalida_NoLaAcepta(int material, int? partida)
        => Assert.Throws<ValidacionComprasException>(() => Reconstruido().AgregarItem(material, 1m, partida, null));

    [Test]
    public void ActualizarItem_ConNull_LimpiaCamposOpcionalesYConservaIdentidadYMaterial()
    {
        var requerimiento = Reconstruido();
        var item = requerimiento.Items.Single();
        var actualizado = requerimiento.ActualizarItem(101, 2.75m, null, null);

        Assert.Multiple(() =>
        {
            Assert.That(actualizado, Is.SameAs(item));
            Assert.That(actualizado.IdRequerimientoDetalle, Is.EqualTo(101));
            Assert.That(actualizado.IdRequerimiento, Is.EqualTo(7));
            Assert.That(actualizado.IdMaterial, Is.EqualTo(20));
            Assert.That(actualizado.Cantidad, Is.EqualTo(2.75m));
            Assert.That(actualizado.IdPresupuestoDetalle, Is.Null);
            Assert.That(actualizado.Observacion, Is.Null);
            Assert.That(requerimiento.Observacion, Is.EqualTo("Cabecera"));
        });
    }

    [Test]
    public void ActualizarItem_ConObservacionInvalida_NoDejaCambiosParciales()
    {
        var requerimiento = Reconstruido();
        Assert.Throws<ValidacionComprasException>(() =>
            requerimiento.ActualizarItem(101, 99m, null, new string('x', 251)));
        var item = requerimiento.Items.Single();
        Assert.That((item.Cantidad, item.IdPresupuestoDetalle, item.Observacion), Is.EqualTo((1.25m, 40, "Línea")));
    }

    [TestCase("actualizar")]
    [TestCase("eliminar")]
    public void GestionItem_ConDetalleAusente_NoModificaElDocumento(string operacion)
    {
        var requerimiento = Reconstruido();
        Assert.Throws<ItemRequerimientoNoEncontradoException>(() =>
        {
            if (operacion == "actualizar") requerimiento.ActualizarItem(999, 2m, null, null);
            else requerimiento.EliminarItem(999);
        });
        Assert.That(requerimiento.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void EliminarItem_UltimoDetalle_ConservaLaSolicitud()
    {
        var requerimiento = Reconstruido();
        Assert.Throws<ConflictoComprasException>(() => requerimiento.EliminarItem(101));
        Assert.That(requerimiento.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void EliminarItem_ConMasDetalles_EliminaSoloElSolicitado()
    {
        var requerimiento = Reconstruido(items: [Detalle(), Detalle(102, 21)]);
        requerimiento.EliminarItem(101);
        Assert.That(requerimiento.Items.Single().IdRequerimientoDetalle, Is.EqualTo(102));
    }

    [TestCase(EstadoRequerimiento.Registrado)]
    [TestCase(EstadoRequerimiento.ValidadoAlmacen)]
    [TestCase(EstadoRequerimiento.AprobadoCoordinador)]
    [TestCase(EstadoRequerimiento.Rechazado)]
    [TestCase(EstadoRequerimiento.EnviadoOC)]
    [TestCase(EstadoRequerimiento.GeneradoOC)]
    [TestCase(EstadoRequerimiento.Anulado)]
    public void GestionItems_FueraDeAlmacen_RechazaTodasLasMutaciones(string estado)
    {
        var requerimiento = Reconstruido(estado, [Detalle(), Detalle(102, 21)]);
        Assert.Throws<ConflictoComprasException>(() => requerimiento.AgregarItem(22, 1m, null, null));
        Assert.Throws<ConflictoComprasException>(() => requerimiento.ActualizarItem(101, 2m, null, null));
        Assert.Throws<ConflictoComprasException>(() => requerimiento.EliminarItem(101));
        Assert.That(requerimiento.Estado, Is.EqualTo(estado));
        Assert.That(requerimiento.Items, Has.Count.EqualTo(2));
    }

    [TestCase("editar")]
    [TestCase("enviar")]
    public void OperacionDelSolicitante_ConOtroUsuario_Rechaza(string operacion)
    {
        var requerimiento = Reconstruido(EstadoRequerimiento.Registrado);
        Assert.Throws<ConflictoComprasException>(() =>
        {
            if (operacion == "editar") requerimiento.ActualizarDatos(99, "REQ-002", FECHA, null, null, null);
            else requerimiento.Enviar(99);
        });
        Assert.That(requerimiento.Estado, Is.EqualTo(EstadoRequerimiento.Registrado));
        Assert.That(requerimiento.NumeroRequerimiento, Is.EqualTo("REQ-001"));
    }

    [Test]
    public void ActualizarDatos_EnBorrador_ConservaContextoIdentidadYAuditoria()
    {
        var requerimiento = Reconstruido(EstadoRequerimiento.Registrado);
        requerimiento.ActualizarDatos(5, " REQ-002 ", FECHA.AddDays(1), " Nueva ", FECHA.AddDays(3), null);
        Assert.Multiple(() =>
        {
            Assert.That(requerimiento.NumeroRequerimiento, Is.EqualTo("REQ-002"));
            Assert.That(requerimiento.FechaRequerimiento, Is.EqualTo(FECHA.AddDays(1)));
            Assert.That(requerimiento.Descripcion, Is.EqualTo("Nueva"));
            Assert.That(requerimiento.FechaEntrega, Is.EqualTo(FECHA.AddDays(3)));
            Assert.That(requerimiento.Observacion, Is.Null);
            Assert.That((requerimiento.IdRequerimiento, requerimiento.IdProyecto, requerimiento.IdUsuarioSolicitante),
                Is.EqualTo((7, 3, 5)));
            Assert.That(requerimiento.FechaCreacion, Is.EqualTo(INSTANTE));
            Assert.That(requerimiento.Items.Single().IdRequerimientoDetalle, Is.EqualTo(101));
        });
    }

    [Test]
    public void ActualizarDatos_ConFechasInvalidas_ConservaLaCabeceraOriginal()
    {
        var requerimiento = Reconstruido(EstadoRequerimiento.Registrado);
        Assert.Throws<ValidacionComprasException>(() =>
            requerimiento.ActualizarDatos(5, "REQ-002", FECHA, "Otra", FECHA.AddDays(-1), null));
        Assert.That((requerimiento.NumeroRequerimiento, requerimiento.Descripcion, requerimiento.Observacion),
            Is.EqualTo(("REQ-001", "Obra", "Cabecera")));
    }

    [Test]
    public void FlujoConforme_RecorreLasEtapasHastaLaOrdenDeCompra()
    {
        var requerimiento = Reconstruido(EstadoRequerimiento.Registrado);
        requerimiento.Enviar(5);
        Assert.That(requerimiento.Estado, Is.EqualTo(EstadoRequerimiento.EnviadoAlmacen));
        requerimiento.ProcesarStock("Conforme", " Revisado ");
        Assert.That(requerimiento.Estado, Is.EqualTo(EstadoRequerimiento.ValidadoAlmacen));
        requerimiento.Aprobar();
        Assert.That(requerimiento.Estado, Is.EqualTo(EstadoRequerimiento.AprobadoCoordinador));
        requerimiento.EnviarCompras();
        Assert.That(requerimiento.Estado, Is.EqualTo(EstadoRequerimiento.EnviadoOC));
        requerimiento.MarcarOrdenCompraGenerada();
        Assert.That(requerimiento.Estado, Is.EqualTo(EstadoRequerimiento.GeneradoOC));
        Assert.That(requerimiento.Observacion, Is.EqualTo("Revisado"));
    }

    [Test]
    public void ProcesarStock_Observado_DevuelveElDocumentoAlSolicitante()
    {
        var requerimiento = Reconstruido();
        requerimiento.ProcesarStock("Observado", "Faltan datos");
        Assert.That(requerimiento.Estado, Is.EqualTo(EstadoRequerimiento.Registrado));
    }

    [Test]
    public void ProcesarStock_ResultadoInvalido_ConservaEstadoYObservacion()
    {
        var requerimiento = Reconstruido();
        Assert.Throws<ValidacionComprasException>(() => requerimiento.ProcesarStock("Otro", "Cambio"));
        Assert.That((requerimiento.Estado, requerimiento.Observacion),
            Is.EqualTo((EstadoRequerimiento.EnviadoAlmacen, "Cabecera")));
    }

    [TestCase(EstadoRequerimiento.ValidadoAlmacen)]
    [TestCase(EstadoRequerimiento.AprobadoCoordinador)]
    public void Rechazar_DesdeCoordinacion_ConservaLasLineas(string estado)
    {
        var requerimiento = Reconstruido(estado);
        requerimiento.Rechazar();
        Assert.That(requerimiento.Estado, Is.EqualTo(EstadoRequerimiento.Rechazado));
        Assert.That(requerimiento.Items, Has.Count.EqualTo(1));
    }

    [TestCase("enviar")]
    [TestCase("procesar")]
    [TestCase("aprobar")]
    [TestCase("rechazar")]
    [TestCase("compras")]
    [TestCase("generar")]
    [TestCase("editar")]
    public void Operacion_DesdeEstadoAnulado_NoReabreElDocumento(string operacion)
    {
        var requerimiento = Reconstruido(EstadoRequerimiento.Anulado);
        Assert.Throws<ConflictoComprasException>(() =>
        {
            switch (operacion)
            {
                case "enviar": requerimiento.Enviar(5); break;
                case "procesar": requerimiento.ProcesarStock("Conforme"); break;
                case "aprobar": requerimiento.Aprobar(); break;
                case "rechazar": requerimiento.Rechazar(); break;
                case "compras": requerimiento.EnviarCompras(); break;
                case "generar": requerimiento.MarcarOrdenCompraGenerada(); break;
                case "editar": requerimiento.ActualizarDatos(5, "Otro", FECHA, null, null, null); break;
            }
        });
        Assert.That(requerimiento.Estado, Is.EqualTo(EstadoRequerimiento.Anulado));
    }

    [Test]
    public void Aprobar_SinRevisionDeAlmacen_NoSaltaEtapas()
        => Assert.Throws<ConflictoComprasException>(() => Reconstruido(EstadoRequerimiento.Registrado).Aprobar());

    [Test]
    public void Enviar_SnapshotHistoricoVacio_NoPermiteContinuar()
    {
        var requerimiento = Reconstruido(EstadoRequerimiento.Registrado, []);
        Assert.Throws<ValidacionComprasException>(() => requerimiento.Enviar(5));
        Assert.That(requerimiento.Estado, Is.EqualTo(EstadoRequerimiento.Registrado));
    }

    [Test]
    public void Items_NoPermiteModificarLaColeccionDesdeFuera()
    {
        var requerimiento = Reconstruido();
        Assert.Throws<NotSupportedException>(() => ((ICollection<ItemRequerimiento>)requerimiento.Items).Clear());
        Assert.That(requerimiento.Items, Has.Count.EqualTo(1));
    }

    private static ItemRequerimiento Detalle(int id = 101, int idMaterial = 20)
        => ItemRequerimiento.Reconstruir(id, 7, idMaterial, 40, 1.25m, "Línea");

    private static Requerimiento Reconstruido(string estado = EstadoRequerimiento.EnviadoAlmacen,
        ItemRequerimiento[]? items = null)
        => Requerimiento.Reconstruir(7, "REQ-001", FECHA, 3, "Obra", FECHA.AddDays(2), 5,
            estado, "Cabecera", INSTANTE, items ?? [Detalle()]);
}
