using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Cobranzas_Vittoria.ControlPresupuestario.Application.ConsultaPresupuestaria.Dashboard;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Excepciones;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Model.Consultas;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Persistence;
using Cobranzas_Vittoria.ControlPresupuestario.Domain.Service;
using Cobranzas_Vittoria.ControlPresupuestario.Presentation.Dto.ConsultaPresupuestaria;

namespace Cobranzas_Vittoria.Tests.Unit.ControlPresupuestario;

public sealed class ObtenerDashboardHandlerTests
{
    private DatosDashboard _datos = null!;
    private ObtenerDashboardHandler _handler = null!;

    [SetUp]
    public void Preparar()
    {
        _datos = new DatosDashboard();
        _handler = new ObtenerDashboardHandler(_datos, _datos, _datos, _datos, new RelojFijo());
    }

    [Test]
    public async Task SinExclusionesOListaVacia_ConservaLaDistribucionOriginal()
    {
        Tablero original = await Obtener();
        Tablero vacio = await Obtener(Array.Empty<int>());

        Assert.That(original.RubrosFiltrados, Is.EqualTo(original.Rubros));
        Assert.That(vacio.RubrosFiltrados, Is.EqualTo(original.Rubros));
        Assert.That(original.Resumen.Ejecutado, Is.EqualTo(180m));
        Assert.That(original.Rubros.Select(r => r.IdCatalogoPartida), Is.EqualTo(new[] { 5, 3, 7, 4 }));
        Assert.That(original.Rubros.Select(r => r.PorcentajeDelTotal), Is.EqualTo(new[] { 55.56m, 33.33m, 16.67m, -5.56m }));
        VerificarInvariantes(original, vacio);
    }

    [Test]
    public async Task ExcluirHoja_RecalculaTodosLosImportesYPorcentajesConLasPartidasIncluidas()
    {
        Tablero tablero = await Obtener(new[] { 3 });

        Assert.That(tablero.RubrosFiltrados.Select(r => r.IdCatalogoPartida), Is.EqualTo(new[] { 5, 7, 4 }));
        Assert.That(tablero.RubrosFiltrados.Sum(r => r.Presupuestado), Is.EqualTo(1600m));
        Assert.That(tablero.RubrosFiltrados.Sum(r => r.Comprometido), Is.EqualTo(160m));
        Assert.That(tablero.RubrosFiltrados.Sum(r => r.Ejecutado), Is.EqualTo(120m));
        Assert.That(tablero.RubrosFiltrados.Select(r => r.PorcentajeDelTotal), Is.EqualTo(new[] { 83.33m, 25m, -8.33m }));
    }

    [Test]
    public async Task ExcluirPadre_EliminaDescendientesYNoRepiteExclusionesDeHijos()
    {
        Tablero soloPadre = await Obtener(new[] { 2 });
        Tablero padreEHijos = await Obtener(new[] { 2, 3, 4, 2, 3 });

        Assert.That(soloPadre.RubrosFiltrados.Select(r => r.IdCatalogoPartida), Is.EqualTo(new[] { 5, 7 }));
        Assert.That(padreEHijos.RubrosFiltrados, Is.EqualTo(soloPadre.RubrosFiltrados));
        Assert.That(soloPadre.RubrosFiltrados.Sum(r => r.Ejecutado), Is.EqualTo(130m));
    }

    [TestCase(1, new[] { 1, 6 })]
    [TestCase(2, new[] { 5, 7, 2 })]
    [TestCase(3, new[] { 5, 7, 4 })]
    [TestCase(9, new[] { 5, 7, 4 })]
    public async Task ExcluirHojaAntesDeAgrupar_RespetaNivelYRamasMenosProfundas(int nivel, int[] idsEsperados)
    {
        Tablero tablero = await Obtener(new[] { 3 }, nivel);

        Assert.That(tablero.RubrosFiltrados.Select(r => r.IdCatalogoPartida), Is.EqualTo(idsEsperados));
        Assert.That(tablero.RubrosFiltrados.Sum(r => r.Ejecutado), Is.EqualTo(120m));
        Assert.That(tablero.RubrosFiltrados.Sum(r => r.Presupuestado), Is.EqualTo(1600m));
        if (nivel == 1)
        {
            Assert.That(tablero.RubrosFiltrados.First().Ejecutado, Is.EqualTo(90m));
            Assert.That(tablero.RubrosFiltrados.First().PorcentajeDelTotal, Is.EqualTo(75m));
        }
    }

    [TestCase(null, 4)]
    [TestCase(1, 1)]
    [TestCase(3, 4)]
    public async Task NavegarRama_ExcluyeAntesDeAgruparYSoloDentroDeLaRama(int? nivel, int idEsperado)
    {
        Tablero tablero = await Obtener(new[] { 3, 5 }, nivel, 2);

        Assert.That(tablero.RubrosFiltrados.Single().IdCatalogoPartida, Is.EqualTo(idEsperado));
        Assert.That(tablero.RubrosFiltrados.Single().Ejecutado, Is.EqualTo(-10m));
        Assert.That(tablero.RubrosFiltrados.Single().PorcentajeDelTotal, Is.EqualTo(100m));
        Assert.That(tablero.Encabezado.Rama.Select(p => p.IdCatalogoPartida), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(tablero.Resumen.Ejecutado, Is.EqualTo(50m));
    }

    [Test]
    public async Task ExcluirAncestroDeLaRamaOVariasRaices_DejaDistribucionVacia()
    {
        Assert.That((await Obtener(new[] { 1 }, padre: 2)).RubrosFiltrados, Is.Empty);
        Assert.That((await Obtener(new[] { 1, 6 })).RubrosFiltrados, Is.Empty);
        Assert.That((await Obtener(new[] { 3, 4, 5, 7 })).RubrosFiltrados, Is.Empty);
    }

    [Test]
    public async Task PartidasExistentesSinMontosOFueraDeRama_NoCambianDistribucion()
    {
        Tablero original = await Obtener(padre: 2);
        Tablero filtrado = await Obtener(new[] { 5, 8 }, padre: 2);

        Assert.That(filtrado.RubrosFiltrados, Is.EqualTo(original.Rubros));
        VerificarInvariantes(original, filtrado);
    }

    [Test]
    public async Task MontosCeroYTotalNetoCero_ConservaRubrosConPorcentajesCero()
    {
        _datos.Filas = new[] { Fila(3, 10), Fila(4, -10), Fila(5, 0), Fila(7, 30) };
        Tablero tablero = await Obtener(new[] { 7 });

        Assert.That(tablero.RubrosFiltrados.Select(r => r.Ejecutado), Is.EqualTo(new[] { 10m, 0m, -10m }));
        Assert.That(tablero.RubrosFiltrados.All(r => r.PorcentajeDelTotal == 0m), Is.True);
    }

    [Test]
    public async Task TotalNegativo_UsaTotalNetoSinConvertirAMontosAbsolutos()
    {
        _datos.Filas = new[] { Fila(3, -20), Fila(4, -10), Fila(5, 0), Fila(7, 100) };
        Tablero tablero = await Obtener(new[] { 7 });

        Assert.That(tablero.RubrosFiltrados.Select(r => r.Ejecutado), Is.EqualTo(new[] { 0m, -10m, -20m }));
        Assert.That(tablero.RubrosFiltrados.Select(r => r.PorcentajeDelTotal), Is.EqualTo(new[] { 0m, 33.33m, 66.67m }));
    }

    [TestCase(null, null)]
    [TestCase(1, null)]
    [TestCase(2, null)]
    [TestCase(null, 1)]
    [TestCase(1, 2)]
    [TestCase(3, 2)]
    public async Task Exclusiones_NoAlteranNingunOtroResultado(int? nivel, int? padre)
    {
        Tablero original = await Obtener(nivel: nivel, padre: padre);
        foreach (int[] exclusiones in new[] { new[] { 3 }, new[] { 2, 3, 2 }, new[] { 1, 6 } })
        {
            Tablero filtrado = await Obtener(exclusiones, nivel, padre);
            VerificarInvariantes(original, filtrado);
        }
    }

    [Test]
    public async Task SinPresupuestosAprobados_DevuelveDistribucionesVacias()
    {
        _datos.Presupuestos = Array.Empty<Presupuesto>();

        Tablero tablero = await Obtener(new[] { 1 });

        Assert.That(tablero.Rubros, Is.Empty);
        Assert.That(tablero.RubrosFiltrados, Is.Empty);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void IdentificadoresNoPositivos_SeValidanEnDtoYDominio(int id)
    {
        ExclusionPartidasRequest request = new() { PartidasExcluidas = new[] { id } };
        List<ValidationResult> errores = new();

        Assert.That(Validator.TryValidateObject(request, new ValidationContext(request), errores, true), Is.False);
        ValidacionPresupuestariaException? error = Assert.ThrowsAsync<ValidacionPresupuestariaException>(
            async () => await Obtener(new[] { id }));
        Assert.That(error!.CodigoError, Is.EqualTo("IDENTIFICADOR_INVALIDO"));
    }

    [Test]
    public void IdentificadorInexistente_UsaLaExcepcionDelCatalogo()
    {
        CatalogoPartidaNoEncontradoException? error = Assert.ThrowsAsync<CatalogoPartidaNoEncontradoException>(
            async () => await Obtener(new[] { 999 }));

        Assert.That(error!.CodigoError, Is.EqualTo("PARTIDA_NO_ENCONTRADA"));
    }

    private Task<Tablero> Obtener(int[]? exclusiones = null, int? nivel = null, int? padre = null)
        => _handler.HandleAsync(new ObtenerDashboardQuery(1, null, nivel, padre, exclusiones));

    private static void VerificarInvariantes(Tablero original, Tablero filtrado)
    {
        // Comparación de la respuesta completa salvo el único miembro que puede cambiar.
        string antes = JsonSerializer.Serialize(original);
        string despues = JsonSerializer.Serialize(filtrado with { RubrosFiltrados = original.RubrosFiltrados });
        Assert.That(despues, Is.EqualTo(antes));
    }

    private static CatalogoPartida Partida(int id, string codigo, int? padre, int nivel, bool hoja = true)
        => CatalogoPartida.Reconstruir(id, codigo, "Partida " + codigo, null, padre, null, null, nivel,
            1, null, null, true, hoja, null, null);

    private static SaldoPartidaFila Fila(int id, decimal ejecutado) => new()
    {
        IdCentroCosto = 1, IdPresupuesto = 10, IdCatalogoPartida = id,
        MontoPresupuestado = id * 100, MontoComprometido = id * 10, MontoEjecutado = ejecutado
    };

    private sealed class RelojFijo : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }

    // Puertos en memoria: montos vigentes y ejecución histórica son fuentes distintas a propósito.
    private sealed class DatosDashboard : ICentroCostoRepository, IPresupuestoRepository,
        ICatalogoPartidaRepository, IConsultaPresupuestariaRepository
    {
        public IReadOnlyList<SaldoPartidaFila> Filas { get; set; } = new[]
        {
            Fila(3, 60), Fila(4, -10), Fila(5, 100), Fila(7, 30)
        };

        public IReadOnlyList<Presupuesto> Presupuestos { get; set; } = new[]
        {
            Presupuesto.Reconstruir(10, "P1", "Presupuesto", null, 1, "CC", "Centro", 1, "PEN", "Soles", "S/",
                new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), true, null, null, 100, null, 1, 0, "APROBADO")
        };

        Task<CentroCosto?> ICentroCostoRepository.ObtenerAsync(int id) => Task.FromResult<CentroCosto?>(
            CentroCosto.Reconstruir(1, "CC", "Centro", null, 1, null, null, null, null, true, null, null));
        Task<IReadOnlyList<Presupuesto>> IPresupuestoRepository.ListarAsync(bool? activo, int? centro, int? moneda, string? busqueda)
            => Task.FromResult(Presupuestos);
        Task<IReadOnlyList<CatalogoPartida>> ICatalogoPartidaRepository.ListarAsync(FiltroPartidas filtro)
            => Task.FromResult<IReadOnlyList<CatalogoPartida>>(new[]
            {
                Partida(1, "1", null, 1, false), Partida(2, "1.1", 1, 2, false),
                Partida(3, "1.1.1", 2, 3), Partida(4, "1.1.2", 2, 3), Partida(5, "1.2", 1, 2),
                Partida(6, "2", null, 1, false), Partida(7, "2.1", 6, 2), Partida(8, "3", null, 1)
            });
        public Task<IReadOnlyList<SaldoPartidaFila>> VigenteAsync(FiltroConsultaPresupuestaria filtro) => Task.FromResult(Filas);
        public Task<IReadOnlyList<EjecucionDiariaFila>> EjecucionDiariaAsync(IReadOnlyCollection<int> ids)
            => Task.FromResult<IReadOnlyList<EjecucionDiariaFila>>(ids.Count == 0 ? Array.Empty<EjecucionDiariaFila>() : new[]
            {
                new EjecucionDiariaFila(10, 3, new DateTime(2026, 2, 1), 70),
                new EjecucionDiariaFila(10, 4, new DateTime(2026, 3, 1), -5),
                new EjecucionDiariaFila(10, 5, new DateTime(2026, 8, 1), 100),
                new EjecucionDiariaFila(10, 7, new DateTime(2026, 2, 1), 20)
            });

        Task<IReadOnlyList<CentroCosto>> ICentroCostoRepository.ListarAsync(bool? activo, int? tipo, int? proyecto, string? busqueda) => throw new NotSupportedException();
        Task<int> ICentroCostoRepository.CrearAsync(CentroCosto centro) => throw new NotSupportedException();
        Task ICentroCostoRepository.ActualizarAsync(CentroCosto centro) => throw new NotSupportedException();
        Task<Presupuesto?> IPresupuestoRepository.ObtenerAsync(int id) => throw new NotSupportedException();
        Task<PresupuestoCreado> IPresupuestoRepository.CrearAsync(Presupuesto presupuesto, string usuario) => throw new NotSupportedException();
        Task IPresupuestoRepository.ActualizarAsync(Presupuesto presupuesto) => throw new NotSupportedException();
        Task<RegistrosAsociadosPresupuesto> IPresupuestoRepository.ContarRegistrosAsociadosAsync(int id) => throw new NotSupportedException();
        Task<CatalogoPartida?> ICatalogoPartidaRepository.ObtenerAsync(int id) => throw new NotSupportedException();
        Task<int> ICatalogoPartidaRepository.CrearAsync(CatalogoPartida partida) => throw new NotSupportedException();
        Task ICatalogoPartidaRepository.ActualizarAsync(CatalogoPartida partida) => throw new NotSupportedException();
        public Task<IReadOnlyList<PresupuestoResumenFila>> ResumenAsync(FiltroConsultaPresupuestaria filtro) => throw new NotSupportedException();
        public Task<IReadOnlyList<PresupuestoVsComprometidoFila>> PresupuestoVsComprometidoAsync(FiltroConsultaPresupuestaria filtro) => throw new NotSupportedException();
        public Task<IReadOnlyList<PresupuestoVsEjecutadoFila>> PresupuestoVsEjecutadoAsync(FiltroConsultaPresupuestaria filtro) => throw new NotSupportedException();
        public Task<IReadOnlyList<SaldoPartidaFila>> SaldoAsync(FiltroConsultaPresupuestaria filtro) => throw new NotSupportedException();
        public Task<IReadOnlyList<GastoPorPartidaFila>> GastosPorPartidaAsync(FiltroConsultaPresupuestaria filtro) => throw new NotSupportedException();
        public Task<IReadOnlyList<GastoPorCentroCostoFila>> GastosPorCentroCostoAsync(FiltroConsultaPresupuestaria filtro) => throw new NotSupportedException();
        public Task<SaldoPartidaFila?> SaldoDetalleAsync(int id) => throw new NotSupportedException();
    }
}
