using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.ControlPresupuestario;

/// <summary>
/// Secciones de gasto en el catálogo de partidas y su importación masiva
/// (usp_CatalogoPartida_CargaMasiva). Respawn vacía los catálogos del módulo
/// entre tests, así que cada test recrea los tipos y secciones que usa.
/// </summary>
[NonParallelizable]
public class CatalogoPartidaSeccionYCargaTests : IntegrationTestBase
{
    private const string Schema = "ControlPresupuestario.";
    private int _tipoMateriales;
    private int _tipoIndirectos;
    private int _seccionOtros;
    private int _seccionTerreno;

    [SetUp]
    public async Task PrepararCatalogos()
    {
        await using var cn = await AbrirConexion();
        await cn.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.TipoPartida (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('MATERIALES'), ('INDIRECTOS')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoPartida t WHERE t.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.SeccionGasto (Codigo, Nombre, Orden)
            SELECT Codigo, Codigo, Orden FROM (VALUES ('OTROS', 1), ('TERRENO', 2)) v(Codigo, Orden)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.SeccionGasto s WHERE s.Codigo = v.Codigo);
            """);
        _tipoMateriales = await Id(cn, "SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'MATERIALES'");
        _tipoIndirectos = await Id(cn, "SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'INDIRECTOS'");
        _seccionOtros = await Id(cn, "SELECT IdSeccionGasto FROM ControlPresupuestario.SeccionGasto WHERE Codigo = 'OTROS'");
        _seccionTerreno = await Id(cn, "SELECT IdSeccionGasto FROM ControlPresupuestario.SeccionGasto WHERE Codigo = 'TERRENO'");
    }

    // ------------------------------------------------------------ alta y edición

    [Test]
    public async Task Crear_HojaConSeccion_LaGuardaYListarFiltraPorSeccion()
    {
        await using var cn = await AbrirConexion();
        var padre = await CrearPartida(cn, "01");
        var hoja = await CrearPartida(cn, "01.01", padre, _seccionTerreno);
        await CrearPartida(cn, "01.02", padre, _seccionOtros);

        var filtradas = (await cn.QueryAsync<Partida>(Schema + "usp_CatalogoPartida_Listar",
            new { IdSeccionGasto = _seccionTerreno }, commandType: CommandType.StoredProcedure)).ToList();

        Assert.That(filtradas.Select(p => p.IdCatalogoPartida), Is.EqualTo(new[] { hoja }));
        Assert.That(filtradas.Single().CodigoSeccionGasto, Is.EqualTo("TERRENO"));
    }

    [Test]
    public async Task Crear_HijaBajoPartidaConSeccion_Rechazada()
    {
        await using var cn = await AbrirConexion();
        var hoja = await CrearPartida(cn, "01", seccion: _seccionOtros);

        Error(51250, async () => { await CrearPartida(cn, "01.01", hoja); });
    }

    [Test]
    public async Task Actualizar_AgrupadoraConSeccion_Rechazada()
    {
        await using var cn = await AbrirConexion();
        var padre = await CrearPartida(cn, "01");
        await CrearPartida(cn, "01.01", padre);

        Error(51250, async () => { await Actualizar(cn, padre, null, _seccionOtros); });
    }

    [Test]
    public async Task Actualizar_SinSeccion_LaQuita()
    {
        await using var cn = await AbrirConexion();
        var hoja = await CrearPartida(cn, "01", seccion: _seccionOtros);

        await Actualizar(cn, hoja, null, null);

        Assert.That(await cn.QuerySingleAsync<int?>(
            "SELECT IdSeccionGasto FROM ControlPresupuestario.CatalogoPartida WHERE IdCatalogoPartida = @hoja",
            new { hoja }), Is.Null);
    }

    // ------------------------------------------------------------ carga masiva

    [Test]
    public async Task CargaMasiva_HijasAntesQuePadres_ResuelveJerarquiaYNiveles()
    {
        await using var cn = await AbrirConexion();
        var filas = Tvp(
            ("A.01.01", _tipoMateriales, "A.01", _seccionOtros),
            ("A.01", _tipoMateriales, "A", null),
            ("A", _tipoIndirectos, null, null));

        var insertadas = await CargaMasiva(cn, filas);

        Assert.That(insertadas, Is.EqualTo(3));
        var partidas = (await cn.QueryAsync<Partida>(Schema + "usp_CatalogoPartida_Listar",
            commandType: CommandType.StoredProcedure)).ToDictionary(p => p.Codigo);
        Assert.That(partidas["A"].Nivel, Is.EqualTo(1));
        Assert.That(partidas["A.01"].Nivel, Is.EqualTo(2));
        Assert.That(partidas["A.01.01"].Nivel, Is.EqualTo(3));
        Assert.That(partidas["A.01.01"].CodigoPartidaPadre, Is.EqualTo("A.01"));
        Assert.That(partidas["A.01.01"].CodigoSeccionGasto, Is.EqualTo("OTROS"));
    }

    [Test]
    public async Task CargaMasiva_HijaDePartidaExistente_UsaSuNivel()
    {
        await using var cn = await AbrirConexion();
        var padre = await CrearPartida(cn, "01");

        await CargaMasiva(cn, Tvp(("01.09", _tipoMateriales, "01", null)));

        var hija = await cn.QuerySingleAsync<Partida>(Schema + "usp_CatalogoPartida_Listar",
            new { IdPartidaPadre = padre }, commandType: CommandType.StoredProcedure);
        Assert.That(hija.Codigo, Is.EqualTo("01.09"));
        Assert.That(hija.Nivel, Is.EqualTo(2));
    }

    [Test]
    public async Task CargaMasiva_Ciclo_RechazaYNoInsertaNada()
    {
        await using var cn = await AbrirConexion();
        var filas = Tvp(
            ("OK", _tipoMateriales, null, null),
            ("X", _tipoMateriales, "Y", null),
            ("Y", _tipoMateriales, "X", null));

        Error(50007, async () => { await CargaMasiva(cn, filas); });
        Assert.That(await Contar(cn), Is.Zero);
    }

    [Test]
    public async Task CargaMasiva_SeccionEnFilaConHijas_Rechazada()
    {
        await using var cn = await AbrirConexion();
        var filas = Tvp(
            ("A", _tipoMateriales, null, _seccionOtros),
            ("A.01", _tipoMateriales, "A", null));

        Error(50006, async () => { await CargaMasiva(cn, filas); });
        Assert.That(await Contar(cn), Is.Zero);
    }

    [Test]
    public async Task CargaMasiva_PadreExistenteConSeccion_Rechazada()
    {
        await using var cn = await AbrirConexion();
        await CrearPartida(cn, "01", seccion: _seccionOtros);

        Error(50005, async () => { await CargaMasiva(cn, Tvp(("01.01", _tipoMateriales, "01", null))); });
        Assert.That(await Contar(cn), Is.EqualTo(1));
    }

    [Test]
    public async Task CargaMasiva_CodigoExistenteOPadreInexistente_Rechazada()
    {
        await using var cn = await AbrirConexion();
        await CrearPartida(cn, "01");

        Error(50003, async () => { await CargaMasiva(cn, Tvp(("01", _tipoMateriales, null, null))); });
        Error(50004, async () => { await CargaMasiva(cn, Tvp(("02", _tipoMateriales, "NOEXISTE", null))); });
        Assert.That(await Contar(cn), Is.EqualTo(1));
    }

    // ------------------------------------------------------------------ helpers

    private static async Task<SqlConnection> AbrirConexion()
    {
        var cn = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        await cn.OpenAsync();
        return cn;
    }

    private static Task<int> Id(SqlConnection cn, string sql) => cn.QuerySingleAsync<int>(sql);

    private static Task<int> Contar(SqlConnection cn) =>
        cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM ControlPresupuestario.CatalogoPartida");

    private async Task<int> CrearPartida(SqlConnection cn, string codigo, int? padre = null, int? seccion = null)
    {
        var fila = await cn.QuerySingleAsync<Partida>(Schema + "usp_CatalogoPartida_Crear",
            new
            {
                Codigo = codigo, Nombre = "Partida " + codigo, IdTipoPartida = _tipoMateriales,
                IdPartidaPadre = padre, IdSeccionGasto = seccion
            }, commandType: CommandType.StoredProcedure);
        return fila.IdCatalogoPartida;
    }

    private Task Actualizar(SqlConnection cn, int id, int? padre, int? seccion) =>
        cn.QuerySingleAsync(Schema + "usp_CatalogoPartida_Actualizar",
            new
            {
                IdCatalogoPartida = id, Nombre = "Editada", IdTipoPartida = _tipoMateriales,
                Activo = true, IdPartidaPadre = padre, IdSeccionGasto = seccion
            }, commandType: CommandType.StoredProcedure);

    private static DataTable Tvp(params (string Codigo, int Tipo, string? Padre, int? Seccion)[] filas)
    {
        // Mismo orden de columnas que ControlPresupuestario.TVP_CatalogoPartida.
        var tabla = new DataTable();
        tabla.Columns.Add("Codigo", typeof(string));
        tabla.Columns.Add("Nombre", typeof(string));
        tabla.Columns.Add("IdTipoPartida", typeof(int));
        tabla.Columns.Add("CodigoPadre", typeof(string));
        tabla.Columns.Add("IdSeccionGasto", typeof(int));
        tabla.Columns.Add("Descripcion", typeof(string));
        tabla.Columns.Add("_Fila", typeof(int));
        var numero = 1;
        foreach (var f in filas)
            tabla.Rows.Add(f.Codigo, "Partida " + f.Codigo, f.Tipo, (object?)f.Padre ?? DBNull.Value,
                (object?)f.Seccion ?? DBNull.Value, DBNull.Value, numero++);
        return tabla;
    }

    private static Task<int> CargaMasiva(SqlConnection cn, DataTable filas) =>
        cn.ExecuteScalarAsync<int>(Schema + "usp_CatalogoPartida_CargaMasiva",
            new { Filas = filas.AsTableValuedParameter("ControlPresupuestario.TVP_CatalogoPartida") },
            commandType: CommandType.StoredProcedure);

    private static void Error(int numero, Func<Task> accion)
    {
        var ex = Assert.ThrowsAsync<SqlException>(async () => await accion());
        Assert.That(ex!.Number, Is.EqualTo(numero));
    }

    private sealed class Partida
    {
        public int IdCatalogoPartida { get; set; }
        public string Codigo { get; set; } = "";
        public int Nivel { get; set; }
        public string? CodigoPartidaPadre { get; set; }
        public string? CodigoSeccionGasto { get; set; }
    }
}
