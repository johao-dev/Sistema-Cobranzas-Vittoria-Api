using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.Maestra;

/// <summary>
/// Partida presupuestal por defecto de los materiales (usp_Material_Upsert y
/// usp_Material_CargaMasiva_v3). Respawn vacía el catálogo de partidas entre
/// tests, así que cada test crea las que usa; maestra.Material se conserva.
/// </summary>
[NonParallelizable]
public class MaterialPartidaDefectoTests : IntegrationTestBase
{
    private int _especialidad;
    private int _padre;
    private int _hoja;

    [SetUp]
    public async Task PrepararPartidas()
    {
        await using var cn = await AbrirConexion();
        _especialidad = await cn.QuerySingleAsync<int>("SELECT TOP (1) IdEspecialidad FROM maestra.Especialidad ORDER BY IdEspecialidad");
        await cn.ExecuteAsync("""
            IF NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'MATERIALES')
                INSERT INTO ControlPresupuestario.TipoPartida (Codigo, Nombre) VALUES ('MATERIALES', N'Materiales');
            """);
        var tipo = await cn.QuerySingleAsync<int>("SELECT IdTipoPartida FROM ControlPresupuestario.TipoPartida WHERE Codigo = 'MATERIALES'");
        _padre = await CrearPartida(cn, tipo, null, 1);
        _hoja = await CrearPartida(cn, tipo, _padre, 2);
    }

    /// <summary>
    /// Respawn conserva maestra.Material pero vacía CatalogoPartida: si un material
    /// quedara apuntando a una partida, la limpieza del siguiente test fallaría por la FK.
    /// </summary>
    [TearDown]
    public async Task SoltarPartidasDeMateriales()
    {
        await using var cn = await AbrirConexion();
        await cn.ExecuteAsync("UPDATE maestra.Material SET IdCatalogoPartida = NULL WHERE IdCatalogoPartida IS NOT NULL;");
    }

    [Test]
    public async Task Upsert_ConPartidaHoja_LaGuardaYLaDevuelveEnListado()
    {
        await using var cn = await AbrirConexion();
        var id = await Upsert(cn, null, _hoja);

        var material = await cn.QuerySingleAsync<MaterialFila>("maestra.usp_Material_Get",
            new { IdMaterial = id }, commandType: CommandType.StoredProcedure);
        Assert.That(material.IdCatalogoPartida, Is.EqualTo(_hoja));
        Assert.That(material.CodigoPartida, Is.Not.Null);

        await Upsert(cn, id, null);
        var editado = await cn.QuerySingleAsync<MaterialFila>("maestra.usp_Material_Get",
            new { IdMaterial = id }, commandType: CommandType.StoredProcedure);
        Assert.That(editado.IdCatalogoPartida, Is.Null, "Editar sin partida la quita.");
    }

    [Test]
    public async Task Upsert_PartidaAgrupadoraOInexistente_Rechazada()
    {
        await using var cn = await AbrirConexion();
        Error(51251, async () => { await Upsert(cn, null, _padre); });
        Error(51251, async () => { await Upsert(cn, null, int.MaxValue); });
    }

    [Test]
    public async Task CargaMasivaV3_AsignaPartidaYRechazaAgrupadora()
    {
        await using var cn = await AbrirConexion();
        var codigo = "QA-" + Guid.NewGuid().ToString("N")[..10];

        var insertadas = await CargaV3(cn, (codigo + "-1", _hoja), (codigo + "-2", null));
        Assert.That(insertadas, Is.EqualTo(2));
        var partidas = (await cn.QueryAsync<(string Codigo, int? IdCatalogoPartida)>(
            "SELECT Codigo, IdCatalogoPartida FROM maestra.Material WHERE Codigo LIKE @p",
            new { p = codigo + "%" })).ToDictionary(x => x.Codigo, x => x.IdCatalogoPartida);
        Assert.That(partidas[codigo + "-1"], Is.EqualTo(_hoja));
        Assert.That(partidas[codigo + "-2"], Is.Null);

        Error(50004, async () => { await CargaV3(cn, (codigo + "-3", _padre)); });
        Assert.That(await cn.QuerySingleAsync<int>("SELECT COUNT(*) FROM maestra.Material WHERE Codigo = @c",
            new { c = codigo + "-3" }), Is.Zero);
    }

    private static async Task<SqlConnection> AbrirConexion()
    {
        var cn = new SqlConnection(GlobalSetupFixture.DbContainer.GetConnectionString());
        await cn.OpenAsync();
        return cn;
    }

    private static Task<int> CrearPartida(SqlConnection cn, int tipo, int? padre, int nivel) =>
        cn.QuerySingleAsync<int>("""
            INSERT INTO ControlPresupuestario.CatalogoPartida (Codigo, Nombre, IdTipoPartida, IdPartidaPadre, Nivel)
            VALUES (@Codigo, N'Partida de pruebas', @tipo, @padre, @nivel);
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { Codigo = Guid.NewGuid().ToString("N"), tipo, padre, nivel });

    private Task<int> Upsert(SqlConnection cn, int? id, int? partida) =>
        cn.ExecuteScalarAsync<int>("maestra.usp_Material_Upsert", new
        {
            IdMaterial = id, IdEspecialidad = _especialidad, Codigo = id is null ? "QA-" + Guid.NewGuid().ToString("N")[..12] : null,
            Descripcion = "Material de pruebas", UnidadMedida = "UND", StockMinimo = 0m, Activo = true,
            IdCatalogoPartida = partida
        }, commandType: CommandType.StoredProcedure);

    private Task<int> CargaV3(SqlConnection cn, params (string Codigo, int? Partida)[] filas)
    {
        // Mismo orden de columnas que maestra.TVP_Material_v3.
        var tabla = new DataTable();
        tabla.Columns.Add("IdEspecialidad", typeof(int));
        tabla.Columns.Add("Codigo", typeof(string));
        tabla.Columns.Add("Descripcion", typeof(string));
        tabla.Columns.Add("IdUnidadMedida", typeof(int));
        tabla.Columns.Add("UnidadMedida", typeof(string));
        tabla.Columns.Add("IdCatalogoPartida", typeof(int));
        tabla.Columns.Add("_Fila", typeof(int));
        var n = 1;
        foreach (var f in filas)
            tabla.Rows.Add(_especialidad, f.Codigo, "Material " + f.Codigo, DBNull.Value, "UND",
                (object?)f.Partida ?? DBNull.Value, n++);
        return cn.ExecuteScalarAsync<int>("maestra.usp_Material_CargaMasiva_v3",
            new { Filas = tabla.AsTableValuedParameter("maestra.TVP_Material_v3") },
            commandType: CommandType.StoredProcedure);
    }

    private static void Error(int numero, Func<Task> accion)
    {
        var ex = Assert.ThrowsAsync<SqlException>(async () => await accion());
        Assert.That(ex!.Number, Is.EqualTo(numero));
    }

    private sealed class MaterialFila
    {
        public int? IdCatalogoPartida { get; set; }
        public string? CodigoPartida { get; set; }
    }
}
