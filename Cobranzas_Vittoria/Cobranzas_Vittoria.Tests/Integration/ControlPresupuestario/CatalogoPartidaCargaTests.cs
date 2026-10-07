using System.Data;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.ControlPresupuestario;

/// <summary>Jerarquía e importación masiva del catálogo presupuestario.</summary>
[NonParallelizable]
public class CatalogoPartidaCargaTests : IntegrationTestBase
{
    private const string Schema = "ControlPresupuestario.";
    private int _tipoMateriales;
    private int _tipoIndirectos;

    [SetUp]
    public async Task PrepararCatalogos()
    {
        await using var cn = await AbrirConexion();
        await cn.ExecuteAsync("""
            INSERT INTO ControlPresupuestario.TipoPartida (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('MATERIALES'), ('INDIRECTOS')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoPartida t WHERE t.Codigo = v.Codigo);
            """);
        _tipoMateriales = await Id(cn, "SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'MATERIALES'");
        _tipoIndirectos = await Id(cn, "SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'INDIRECTOS'");
    }

    [Test]
    public async Task Crear_PadreEHija_ConservaJerarquiaYNiveles()
    {
        await using var cn = await AbrirConexion();
        var padre = await CrearPartida(cn, "01");
        var hija = await CrearPartida(cn, "01.01", padre);

        var partidas = (await cn.QueryAsync<Partida>(Schema + "usp_CatalogoPartida_Listar",
            commandType: CommandType.StoredProcedure)).ToDictionary(p => p.IdCatalogoPartida);
        Assert.That(partidas[padre].Nivel, Is.EqualTo(1));
        Assert.That(partidas[hija].Nivel, Is.EqualTo(2));
        Assert.That(partidas[hija].CodigoPartidaPadre, Is.EqualTo("01"));
    }

    [Test]
    public async Task CargaMasiva_HijasAntesQuePadres_ResuelveJerarquiaYNiveles()
    {
        await using var cn = await AbrirConexion();
        var filas = Tvp(
            ("A.01.01", _tipoMateriales, "A.01"),
            ("A.01", _tipoMateriales, "A"),
            ("A", _tipoIndirectos, null));

        Assert.That(await CargaMasiva(cn, filas), Is.EqualTo(3));
        var partidas = (await cn.QueryAsync<Partida>(Schema + "usp_CatalogoPartida_Listar",
            commandType: CommandType.StoredProcedure)).ToDictionary(p => p.Codigo);
        Assert.That(partidas["A"].Nivel, Is.EqualTo(1));
        Assert.That(partidas["A.01"].Nivel, Is.EqualTo(2));
        Assert.That(partidas["A.01.01"].Nivel, Is.EqualTo(3));
        Assert.That(partidas["A.01.01"].CodigoPartidaPadre, Is.EqualTo("A.01"));
    }

    [Test]
    public async Task CargaMasiva_HijaDePartidaExistente_UsaSuNivel()
    {
        await using var cn = await AbrirConexion();
        var padre = await CrearPartida(cn, "01");
        await CargaMasiva(cn, Tvp(("01.09", _tipoMateriales, "01")));

        var hija = await cn.QuerySingleAsync<Partida>(Schema + "usp_CatalogoPartida_Listar",
            new { IdPartidaPadre = padre }, commandType: CommandType.StoredProcedure);
        Assert.That((hija.Codigo, hija.Nivel), Is.EqualTo(("01.09", 2)));
    }

    [Test]
    public async Task CargaMasiva_Ciclo_RechazaYNoInsertaNada()
    {
        await using var cn = await AbrirConexion();
        var filas = Tvp(("OK", _tipoMateriales, null), ("X", _tipoMateriales, "Y"), ("Y", _tipoMateriales, "X"));

        Error(50007, async () => { await CargaMasiva(cn, filas); });
        Assert.That(await Contar(cn), Is.Zero);
    }

    [Test]
    public async Task CargaMasiva_CodigoExistenteOPadreInexistente_Rechazada()
    {
        await using var cn = await AbrirConexion();
        await CrearPartida(cn, "01");

        Error(50003, async () => { await CargaMasiva(cn, Tvp(("01", _tipoMateriales, null))); });
        Error(50004, async () => { await CargaMasiva(cn, Tvp(("02", _tipoMateriales, "NOEXISTE"))); });
        Assert.That(await Contar(cn), Is.EqualTo(1));
    }

    private static async Task<SqlConnection> AbrirConexion()
    {
        var cn = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        await cn.OpenAsync();
        return cn;
    }

    private static Task<int> Id(SqlConnection cn, string sql) => cn.QuerySingleAsync<int>(sql);

    private static Task<int> Contar(SqlConnection cn) =>
        cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM ControlPresupuestario.CatalogoPartida");

    private async Task<int> CrearPartida(SqlConnection cn, string codigo, int? padre = null)
    {
        var fila = await cn.QuerySingleAsync<Partida>(Schema + "usp_CatalogoPartida_Crear",
            new { Codigo = codigo, Nombre = "Partida " + codigo, IdTipoPartida = _tipoMateriales, IdPartidaPadre = padre },
            commandType: CommandType.StoredProcedure);
        return fila.IdCatalogoPartida;
    }

    private static DataTable Tvp(params (string Codigo, int Tipo, string? Padre)[] filas)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("Codigo", typeof(string));
        tabla.Columns.Add("Nombre", typeof(string));
        tabla.Columns.Add("IdTipoPartida", typeof(int));
        tabla.Columns.Add("CodigoPadre", typeof(string));
        tabla.Columns.Add("Descripcion", typeof(string));
        tabla.Columns.Add("_Fila", typeof(int));
        var numero = 1;
        foreach (var f in filas)
            tabla.Rows.Add(f.Codigo, "Partida " + f.Codigo, f.Tipo, (object?)f.Padre ?? DBNull.Value,
                DBNull.Value, numero++);
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
        public string Codigo { get; set; } = string.Empty;
        public int Nivel { get; set; }
        public string? CodigoPartidaPadre { get; set; }
    }
}
