using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.GastosPorCentroCosto;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.GastosPorPartida;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.PresupuestoVsComprometido;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.PresupuestoVsEjecutado;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Resumen;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Saldo;
using Cobranzas_Vittoria.ControlPresupuestario.Application.MovimientoPresupuestal.ListarConsolidados;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;

namespace Cobranzas_Vittoria.Tests.Unit.ControlPresupuestario;

public sealed class ConsultaConsolidadaTests
{
    [TestCase(null, "APROBADO")]
    [TestCase(null, "HISTORICO")]
    [TestCase(null, "borrador")]
    [TestCase(null, "ANULADO")]
    [TestCase(12, null)]
    [TestCase(12, "APROBADO")]
    public void Consolidado_ValidaFiltrosAntesDeAplicarDefaults(int? version, string? estado)
    {
        ConsultaPresupuestariaQuery query = new(null, null, version, null, null, estado, Consolidado: true);
        ValidacionPresupuestariaException? error = Assert.Throws<ValidacionPresupuestariaException>(() => query.AFiltro());
        Assert.That(error!.CodigoError, Is.EqualTo("FILTROS_INCOMPATIBLES"));
    }

    [Test]
    public void Filtros_ConservanDefaultsHistoricosYAlcanceConsolidado()
    {
        ConsultaPresupuestariaQuery query = new(1, 10, null, 25, 2, null, Consolidado: true);
        Assert.That(query.AFiltro(), Is.EqualTo(new FiltroConsultaPresupuestaria(1, 10, null, 25, 2, "APROBADO", true)));
        Assert.That((query with { IncluirInactivos = true }).AFiltro().SoloPresupuestosActivos, Is.False);
        Assert.That((query with { Consolidado = false, IdPresupuestoVersion = 11 }).AFiltro().EstadoPresupuesto, Is.Null);
        Assert.That((query with { Consolidado = false, EstadoPresupuesto = "historico" }).AFiltro().EstadoPresupuesto,
            Is.EqualTo("HISTORICO"));
    }

    [TestCase("COMPROMISO", "COMPROMISO", "INCREMENTO", 25, 0)]
    [TestCase("LIBERACION", "COMPROMISO", "DECREMENTO", -25, 0)]
    [TestCase("EJECUCION", "EJECUCION", "INCREMENTO", 0, 25)]
    [TestCase("AJUSTE", "COMPROMISO", "INCREMENTO", 25, 0)]
    [TestCase("AJUSTE", "COMPROMISO", "DECREMENTO", -25, 0)]
    [TestCase("AJUSTE", "EJECUCION", "INCREMENTO", 0, 25)]
    [TestCase("AJUSTE", "EJECUCION", "DECREMENTO", 0, -25)]
    [TestCase("AJUSTE", "EJECUCION", null, 0, 0)]
    public void Movimiento_ConservaImporteOriginalYCalculaEfectos(string tipo, string afectacion, string? direccion,
        decimal compromiso, decimal ejecucion)
    {
        MovimientoPresupuestal movimiento = MovimientoPresupuestal.Reconstruir(1, 1, 1, tipo, tipo, "evento", "PRUEBA", 1,
            afectacion, direccion, new DateTime(2026, 1, 1), 25m, null, 10, 11, 1, "HISTORICO", 25,
            "1", "Partida", "P1", 1, "PEN", "S/", false);

        MovimientoConsolidadoResult resultado = MovimientoConsolidadoResult.Desde(movimiento);

        Assert.That(resultado.Movimiento.Monto, Is.EqualTo(25m));
        Assert.That(resultado.EfectoComprometido, Is.EqualTo(compromiso));
        Assert.That(resultado.EfectoEjecutado, Is.EqualTo(ejecucion));
        Assert.That(resultado.Movimiento.EstadoPresupuesto, Is.EqualTo("HISTORICO"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SeisReportes_UsanFuenteSolicitadaSinCambiarFiltros(bool consolidado)
    {
        RepositorioConsultas repository = new();
        ConsultaPresupuestariaQuery query = new(1, 10, null, 25, 1, null, Consolidado: consolidado);

        await new ResumenHandler(repository).HandleAsync(query);
        await new SaldoHandler(repository).HandleAsync(query);
        await new PresupuestoVsComprometidoHandler(repository).HandleAsync(query);
        await new PresupuestoVsEjecutadoHandler(repository).HandleAsync(query);
        await new GastosPorPartidaHandler(repository).HandleAsync(query);
        await new GastosPorCentroCostoHandler(repository).HandleAsync(query);

        Assert.That(repository.ConsultasVigentes, Is.EqualTo(consolidado ? 6 : 0));
        Assert.That(repository.ConsultasIndividuales, Is.EqualTo(consolidado ? 0 : 6));
        Assert.That(repository.UltimoFiltro, Is.EqualTo(query.AFiltro()));
    }

    [Test]
    public void MonedasMixtas_ImpidenAgregacionConsolidada()
    {
        RepositorioConsultas repository = new() { Filas = new[] { Fila(1, 1), Fila(2, 2) } };
        ConsultaPresupuestariaQuery query = new(null, null, null, null, null, null, Consolidado: true);
        ValidacionPresupuestariaException? error = Assert.ThrowsAsync<ValidacionPresupuestariaException>(
            async () => await new ResumenHandler(repository).HandleAsync(query));
        Assert.That(error!.CodigoError, Is.EqualTo("MONEDAS_MIXTAS"));
    }

    [Test]
    public async Task Resumen_SumaAntesDeCalcularPorcentajeYConservaExcesoConPresupuestoCero()
    {
        RepositorioConsultas repository = new() { Filas = new[]
        {
            Fila(1, 1) with { MontoPresupuestado = 100m, MontoEjecutado = 125m, SaldoDisponible = -25m },
            Fila(2, 1) with { MontoPresupuestado = 0m, MontoEjecutado = 25m, SaldoDisponible = -25m }
        } };
        ConsultaPresupuestariaQuery query = new(null, null, null, null, null, null, Consolidado: true);
        ResumenResult resumen = await new ResumenHandler(repository).HandleAsync(query);
        Assert.That(resumen.Totales.PorcentajeEjecutado, Is.EqualTo(150m));
        Assert.That(resumen.PartidasExcedidas, Has.Count.EqualTo(2));
        Assert.That(resumen.PorCentroCosto.Single().PorcentajeEjecutado, Is.EqualTo(150m));
        IReadOnlyList<GastoPorPartidaFila> gastos = await new GastosPorPartidaHandler(repository).HandleAsync(query);
        Assert.That(gastos.Single(f => f.IdCatalogoPartida == 2).PorcentajeEjecutado, Is.Zero);
    }

    private static SaldoPartidaFila Fila(int partida, int moneda) => new()
    {
        IdCentroCosto = 1, IdPresupuesto = 10, IdPresupuestoVersion = 12, IdPresupuestoDetalle = partida,
        IdCatalogoPartida = partida, IdMoneda = moneda, NumeroVersion = 3, EstadoPresupuesto = "APROBADO"
    };

    private sealed class RepositorioConsultas : IConsultaPresupuestariaRepository
    {
        public IReadOnlyList<SaldoPartidaFila> Filas { get; init; } = Array.Empty<SaldoPartidaFila>();
        public int ConsultasVigentes { get; private set; }
        public int ConsultasIndividuales { get; private set; }
        public FiltroConsultaPresupuestaria? UltimoFiltro { get; private set; }

        public Task<IReadOnlyList<SaldoPartidaFila>> VigenteAsync(FiltroConsultaPresupuestaria filtro)
        {
            ConsultasVigentes++;
            UltimoFiltro = filtro;
            return Task.FromResult(Filas);
        }

        private Task<IReadOnlyList<T>> Individual<T>(FiltroConsultaPresupuestaria filtro)
        {
            ConsultasIndividuales++;
            UltimoFiltro = filtro;
            return Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
        }

        public Task<IReadOnlyList<PresupuestoResumenFila>> ResumenAsync(FiltroConsultaPresupuestaria filtro) => Individual<PresupuestoResumenFila>(filtro);
        public Task<IReadOnlyList<SaldoPartidaFila>> SaldoAsync(FiltroConsultaPresupuestaria filtro) => Individual<SaldoPartidaFila>(filtro);
        public Task<IReadOnlyList<PresupuestoVsComprometidoFila>> PresupuestoVsComprometidoAsync(FiltroConsultaPresupuestaria filtro) => Individual<PresupuestoVsComprometidoFila>(filtro);
        public Task<IReadOnlyList<PresupuestoVsEjecutadoFila>> PresupuestoVsEjecutadoAsync(FiltroConsultaPresupuestaria filtro) => Individual<PresupuestoVsEjecutadoFila>(filtro);
        public Task<IReadOnlyList<GastoPorPartidaFila>> GastosPorPartidaAsync(FiltroConsultaPresupuestaria filtro) => Individual<GastoPorPartidaFila>(filtro);
        public Task<IReadOnlyList<GastoPorCentroCostoFila>> GastosPorCentroCostoAsync(FiltroConsultaPresupuestaria filtro) => Individual<GastoPorCentroCostoFila>(filtro);
        public Task<IReadOnlyList<EjecucionDiariaFila>> EjecucionDiariaAsync(IReadOnlyCollection<int> idsPresupuesto) => throw new NotSupportedException();
        public Task<SaldoPartidaFila?> SaldoDetalleAsync(int idPresupuestoDetalle) => throw new NotSupportedException();
    }
}
