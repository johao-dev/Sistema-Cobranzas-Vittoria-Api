using Cobranzas_Vittoria.ControlPresupuestario.Domain.Service;

namespace Cobranzas_Vittoria.Tests.Unit.ControlPresupuestario;

/// <summary>Cálculo del dashboard: distribución por rubro y curva acumulada real vs presupuesto.</summary>
public class TableroPresupuestarioTests
{
    private static readonly DateTime Inicio = new(2026, 1, 1);
    private static readonly DateTime Fin = new(2026, 12, 31);

    private static EncabezadoTablero Encabezado(DateTime? inicio, DateTime? fin)
        => new(1, "CC", "Obra", null, 1, "PEN", "S/", 1, 1, inicio, fin);

    private static RubroTablero Rubro(int id, string codigo, decimal presupuestado, decimal ejecutado)
        => new(id, codigo, "Partida " + codigo, presupuestado, 0m, ejecutado);

    private static EjecucionTablero Gasto(string fecha, int partida, decimal monto)
        => new(DateTime.Parse(fecha), partida, monto);

    [Test]
    public void Distribucion_OrdenaPorGastoYCalculaElPorcentajeDelTotal()
    {
        var d = TableroPresupuestario.Construir(Encabezado(Inicio, Fin),
            new[] { Rubro(1, "A", 100, 25), Rubro(2, "B", 100, 75) },
            new[] { Gasto("2026-02-01", 1, 25), Gasto("2026-03-01", 2, 75) }, new DateTime(2026, 6, 1));

        Assert.That(d.Rubros.Select(r => r.Codigo), Is.EqualTo(new[] { "B", "A" }));
        Assert.That(d.Rubros.Select(r => r.PorcentajeDelTotal), Is.EqualTo(new[] { 75m, 25m }));
    }

    [Test]
    public void PresupuestoAcumulado_EsLinealEntreInicioYFin()
    {
        var fin = Inicio.AddDays(100);
        var d = TableroPresupuestario.Construir(Encabezado(Inicio, fin),
            new[] { Rubro(1, "A", 1000, 0) }, Array.Empty<EjecucionTablero>(), Inicio.AddDays(50));

        Assert.That(d.AlCorte.PresupuestoAcumulado, Is.EqualTo(500m), "A mitad del período va el 50 %.");
        Assert.That(d.Semanas.First().PresupuestoAcumulado, Is.Zero);
        Assert.That(d.Semanas.Last().PresupuestoAcumulado, Is.EqualTo(1000m));
    }

    [Test]
    public void GastoReal_SeAcumulaYSoloSeDibujaHastaLaFechaDeCorte()
    {
        var hoy = new DateTime(2026, 3, 1);
        var d = TableroPresupuestario.Construir(Encabezado(Inicio, Fin),
            new[] { Rubro(1, "A", 1000, 300) },
            new[] { Gasto("2026-01-10", 1, 100), Gasto("2026-02-10", 1, 200) }, hoy);

        Assert.That(d.AlCorte.RealAcumulado, Is.EqualTo(300m));
        Assert.That(d.Semanas.Where(s => s.Fecha > hoy).All(s => s.RealAcumulado is null), Is.True);
        var reales = d.Semanas.Where(s => s.RealAcumulado is not null).Select(s => s.RealAcumulado!.Value).ToList();
        Assert.That(reales, Is.Ordered, "El acumulado nunca baja sin un ajuste.");
        Assert.That(d.Semanas.Any(s => s.Fecha == hoy), Is.True, "El último punto real es la fecha de corte.");
    }

    [Test]
    public void GastoConFechaFutura_MueveLaFechaDeCorteParaNoPerderlo()
    {
        var d = TableroPresupuestario.Construir(Encabezado(Inicio, Fin),
            new[] { Rubro(1, "A", 1000, 300) },
            new[] { Gasto("2026-02-01", 1, 100), Gasto("2026-10-01", 1, 200) }, new DateTime(2026, 9, 28));

        Assert.That(d.AlCorte.RealAcumulado, Is.EqualTo(300m), "Coincide con el ejecutado total.");
    }

    [Test]
    public void SinFechasDelPresupuesto_NoHayCurvaPresupuestadaNiDesviacion()
    {
        var d = TableroPresupuestario.Construir(Encabezado(null, null),
            new[] { Rubro(1, "A", 1000, 100) }, new[] { Gasto("2026-02-01", 1, 100) }, new DateTime(2026, 3, 1));

        Assert.That(d.Semanas.All(s => s.PresupuestoAcumulado is null), Is.True);
        Assert.That(d.AlCorte.Desviacion, Is.Null);
        Assert.That(d.DesviacionPorRubro, Is.Empty);
        Assert.That(d.AlCorte.RealAcumulado, Is.EqualTo(100m));
    }

    [Test]
    public void DesviacionPorRubro_ComparaRealContraPresupuestoAcumuladoALaFecha()
    {
        var fin = Inicio.AddDays(100);
        var d = TableroPresupuestario.Construir(Encabezado(Inicio, fin),
            new[] { Rubro(1, "FIERRO", 1000, 600), Rubro(2, "CEMENTO", 1000, 400) },
            new[] { Gasto("2026-01-20", 1, 600), Gasto("2026-01-20", 2, 400) }, Inicio.AddDays(50));

        var fierro = d.DesviacionPorRubro.Single(r => r.Codigo == "FIERRO");
        Assert.That(fierro.PresupuestoAcumulado, Is.EqualTo(500m));
        Assert.That(fierro.Desviacion, Is.EqualTo(100m));
        Assert.That(fierro.DesviacionPorcentaje, Is.EqualTo(20m));
        Assert.That(d.DesviacionPorRubro.First().Codigo, Is.EqualTo("FIERRO"), "Primero el que más se desvía.");
        Assert.That(d.AlCorte.Desviacion, Is.EqualTo(0m));
    }

    [Test]
    public void SinFechas_LaCurvaRealParteDeCeroAntesDelPrimerGasto()
    {
        // Caso reportado: presupuesto sin fechas, un solo gasto de 2.8 M confirmado hoy.
        var hoy = new DateTime(2026, 9, 30);
        var d = TableroPresupuestario.Construir(Encabezado(null, null),
            new[] { Rubro(1, "01.01", 3_000_000, 2_800_000) }, new[] { Gasto("2026-09-30", 1, 2_800_000) }, hoy);

        Assert.That(d.Semanas.Count, Is.GreaterThanOrEqualTo(2), "Una curva necesita al menos dos puntos.");
        Assert.That(d.Semanas.First().RealAcumulado, Is.Zero);
        Assert.That(d.Semanas.Last(s => s.RealAcumulado is not null).RealAcumulado, Is.EqualTo(2_800_000m));
        Assert.That(d.AlCorte.RealAcumulado, Is.EqualTo(2_800_000m));
    }

    [Test]
    public void GastoAnteriorAlInicioDelPresupuesto_SeDibujaYElPlanSigueSusFechas()
    {
        var d = TableroPresupuestario.Construir(Encabezado(new DateTime(2026, 10, 1), new DateTime(2026, 12, 31)),
            new[] { Rubro(1, "A", 900, 300) }, new[] { Gasto("2026-09-15", 1, 300) }, new DateTime(2026, 9, 20));

        Assert.That(d.Semanas.First().RealAcumulado, Is.Zero);
        Assert.That(d.Semanas.Where(s => s.RealAcumulado is not null).Max(s => s.RealAcumulado), Is.EqualTo(300m),
            "El gasto previo al inicio no desaparece de la curva.");
        Assert.That(d.Semanas.Where(s => s.Fecha <= new DateTime(2026, 10, 1)).All(s => s.PresupuestoAcumulado == 0m), Is.True);
        Assert.That(d.Semanas.Last().PresupuestoAcumulado, Is.EqualTo(900m));
    }
}
