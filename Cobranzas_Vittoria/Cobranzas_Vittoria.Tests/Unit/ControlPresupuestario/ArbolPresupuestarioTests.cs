using Cobranzas_Vittoria.ControlPresupuestario.Domain.Service;

namespace Cobranzas_Vittoria.Tests.Unit.ControlPresupuestario;

/// <summary>Árbol de partidas: cada categoría suma sus hojas descendientes una sola vez, en preorden natural.</summary>
public class ArbolPresupuestarioTests
{
    // 1 Directos ─┬─ 1.1 Estructuras ─┬─ 1.1.1 Concreto
    //             │                   └─ 1.1.2 Acero
    //             └─ 1.10 Acabados
    // 2 Indirectos ── 2.1 Terreno
    private static readonly PartidaArbol[] Catalogo =
    {
        new(1, "1", "Directos", null, 1),
        new(2, "1.1", "Estructuras", 1, 2),
        new(3, "1.1.1", "Concreto", 2, 3),
        new(4, "1.1.2", "Acero", 2, 3),
        new(5, "1.10", "Acabados", 1, 2),
        new(6, "2", "Indirectos", null, 1),
        new(7, "2.1", "Terreno", 6, 2),
        new(8, "3", "Sin montos", null, 1),
    };

    private static MontosHojaArbol Hoja(int partida, decimal presupuestado, decimal comprometido, decimal ejecutado,
        int presupuesto = 1, int detalle = 0)
        => new(partida, presupuesto, 10, detalle == 0 ? partida * 100 : detalle, presupuestado, comprometido, ejecutado);

    [Test]
    public void Categorias_SumanSusHojasDescendientes()
    {
        var (totales, nodos) = ArbolPresupuestario.Construir(new[]
        {
            Hoja(3, 100, 10, 50), Hoja(4, 200, 0, 20), Hoja(5, 300, 0, 0), Hoja(7, 1000, 0, 900)
        }, Catalogo);
        var n = nodos.ToDictionary(x => x.Codigo);

        Assert.That(n["1.1"].MontoPresupuestado, Is.EqualTo(300m));
        Assert.That(n["1.1"].MontoEjecutado, Is.EqualTo(70m));
        Assert.That(n["1"].MontoPresupuestado, Is.EqualTo(600m));
        Assert.That(n["1"].MontoComprometido, Is.EqualTo(10m));
        Assert.That(n["1"].SaldoDisponible, Is.EqualTo(520m));
        Assert.That(n["1"].CantidadHijas, Is.EqualTo(2));
        Assert.That(n["1"].CantidadHojas, Is.EqualTo(3));
        Assert.That(n["2"].PorcentajeEjecutado, Is.EqualTo(90m));
        Assert.That(totales.MontoPresupuestado, Is.EqualTo(1600m), "El total no duplica los montos de las categorías.");
        Assert.That(totales.MontoEjecutado, Is.EqualTo(970m));
        Assert.That(totales.CantidadPartidas, Is.EqualTo(7));
    }

    [Test]
    public void Nodos_VanEnPreordenConOrdenNaturalYSinRamasVacias()
    {
        var (_, nodos) = ArbolPresupuestario.Construir(new[] { Hoja(3, 1, 0, 0), Hoja(4, 1, 0, 0), Hoja(5, 1, 0, 0), Hoja(7, 1, 0, 0) },
            Catalogo);

        Assert.That(nodos.Select(x => x.Codigo), Is.EqualTo(new[] { "1", "1.1", "1.1.1", "1.1.2", "1.10", "2", "2.1" }),
            "1.10 va después de 1.1 y la raíz 3, sin montos, no aparece.");
        Assert.That(nodos.Where(x => x.EsHoja).Select(x => x.Codigo), Is.EqualTo(new[] { "1.1.1", "1.1.2", "1.10", "2.1" }));
        Assert.That(nodos.Single(x => x.Codigo == "1.1.1").IdPartidaPadre, Is.EqualTo(2));
        Assert.That(nodos.Single(x => x.Codigo == "1").IdPartidaPadre, Is.Null);
    }

    [Test]
    public void Excedidas_SeCuentanEnCadaAncestroAunqueElAgregadoNoExceda()
    {
        var (totales, nodos) = ArbolPresupuestario.Construir(new[] { Hoja(3, 100, 0, 150), Hoja(4, 1000, 0, 0) }, Catalogo);
        var n = nodos.ToDictionary(x => x.Codigo);

        Assert.That(n["1.1.1"].Excedido, Is.True);
        Assert.That(n["1.1"].Excedido, Is.False);
        Assert.That(n["1.1"].PartidasExcedidas, Is.EqualTo(1));
        Assert.That(n["1"].PartidasExcedidas, Is.EqualTo(1));
        Assert.That(totales.PartidasExcedidas, Is.EqualTo(1));
    }

    [Test]
    public void Hoja_ConUnDetalleLoIdentifica_ConVariosPresupuestosLoDejaNulo()
    {
        var (_, nodos) = ArbolPresupuestario.Construir(new[]
        {
            Hoja(3, 100, 0, 0, presupuesto: 1, detalle: 31),
            Hoja(4, 100, 0, 0, presupuesto: 1, detalle: 41), Hoja(4, 50, 0, 0, presupuesto: 2, detalle: 42)
        }, Catalogo);
        var n = nodos.ToDictionary(x => x.Codigo);

        Assert.That((n["1.1.1"].IdPresupuesto, n["1.1.1"].IdPresupuestoDetalle), Is.EqualTo((1, 31)));
        Assert.That(n["1.1.2"].IdPresupuestoDetalle, Is.Null);
        Assert.That(n["1.1.2"].MontoPresupuestado, Is.EqualTo(150m));
        Assert.That(n["1.1"].IdPresupuestoDetalle, Is.Null, "Las categorías no tienen detalle.");
    }

    [Test]
    public void SinMontos_DevuelveArbolVacio()
    {
        var (totales, nodos) = ArbolPresupuestario.Construir(Array.Empty<MontosHojaArbol>(), Catalogo);

        Assert.That(nodos, Is.Empty);
        Assert.That(totales.MontoPresupuestado, Is.Zero);
        Assert.That(totales.PorcentajeEjecutado, Is.Zero);
    }

    [TestCase("1.2", "1.10", -1)]
    [TestCase("01.02", "01.10", -1)]
    [TestCase("1", "1.1", -1)]
    [TestCase("A.2", "a.2", 0)]
    public void CompararCodigos_EsNaturalPorSegmentos(string a, string b, int signo)
        => Assert.That(Math.Sign(ArbolPresupuestario.CompararCodigos(a, b)), Is.EqualTo(signo));
}
