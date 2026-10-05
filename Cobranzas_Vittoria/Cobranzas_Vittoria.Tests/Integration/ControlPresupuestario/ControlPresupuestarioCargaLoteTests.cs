using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Integration.ControlPresupuestario;

// Carga en lote de montos (usp_PresupuestoDetalle_CargaLote): misma fixture que el núcleo.
public partial class ControlPresupuestarioSpsTests
{
    [Test]
    public async Task CargaLote_AgregaActualizaYConservaObservacionNula()
    {
        await using var cn = await AbrirConexion();
        var p = await Crear(cn);
        var otra = await CrearPartida(cn);
        await AgregarPorSp(cn, p.IdPresupuestoVersion, _idPartida, 5m);
        await cn.ExecuteAsync("UPDATE ControlPresupuestario.PresupuestoDetalle SET Observacion = N'original' WHERE IdPresupuestoVersion = @v",
            new { v = p.IdPresupuestoVersion });

        var r = await CargaLote(cn, p.IdPresupuestoVersion, Lote((_idPartida, 10m, null), (otra, 0m, "nueva")));

        Assert.That((r.Agregados, r.Actualizados, r.Eliminados), Is.EqualTo((1, 1, 0)));
        Assert.That(r.MontoTotal, Is.EqualTo(10m));
        var detalles = await Detalles(cn, p.IdPresupuestoVersion);
        Assert.That(detalles[_idPartida].MontoPresupuestado, Is.EqualTo(10m));
        Assert.That(detalles[_idPartida].Observacion, Is.EqualTo("original"));
        Assert.That(detalles[otra].MontoPresupuestado, Is.Zero);
        Assert.That(detalles[otra].Observacion, Is.EqualTo("nueva"));
    }

    [Test]
    public async Task CargaLote_QuitarAusentes_EliminaLasQueNoVienen()
    {
        await using var cn = await AbrirConexion();
        var p = await Crear(cn);
        var otra = await CrearPartida(cn);
        await AgregarPorSp(cn, p.IdPresupuestoVersion, _idPartida, 5m);
        await AgregarPorSp(cn, p.IdPresupuestoVersion, otra, 7m);

        var r = await CargaLote(cn, p.IdPresupuestoVersion, Lote((otra, 8m, null)), quitarAusentes: true);

        Assert.That((r.Actualizados, r.Eliminados, r.PartidasEnVersion), Is.EqualTo((1, 1, 1)));
        Assert.That((await Detalles(cn, p.IdPresupuestoVersion)).Keys, Is.EqualTo(new[] { otra }));
    }

    [Test]
    public async Task CargaLote_VersionAprobada_Rechazada()
    {
        await using var cn = await AbrirConexion();
        var p = await Crear(cn);
        await AgregarPorSp(cn, p.IdPresupuestoVersion, _idPartida, 5m);
        await Aprobar(cn, p.IdPresupuestoVersion);

        Error(51207, async () => { await CargaLote(cn, p.IdPresupuestoVersion, Lote((_idPartida, 1m, null))); });
        Assert.That((await Detalles(cn, p.IdPresupuestoVersion))[_idPartida].MontoPresupuestado, Is.EqualTo(5m));
    }

    [Test]
    public async Task CargaLote_FilaInvalida_NoCargaNada()
    {
        await using var cn = await AbrirConexion();
        var p = await Crear(cn);
        var hija = await CrearPartida(cn);
        await cn.ExecuteAsync("""
            UPDATE ControlPresupuestario.CatalogoPartida SET IdPartidaPadre = @padre, Nivel = 2
            WHERE IdCatalogoPartida = @hija;
            """, new { padre = _idPartida, hija });
        var otra = await CrearPartida(cn);

        Error(51212, async () => { await CargaLote(cn, p.IdPresupuestoVersion, Lote((otra, 1m, null), (_idPartida, 1m, null))); });
        Error(51221, async () => { await CargaLote(cn, p.IdPresupuestoVersion, Lote((otra, -1m, null))); });
        Error(51215, async () => { await CargaLote(cn, p.IdPresupuestoVersion, Lote((otra, 1m, null), (otra, 2m, null))); });
        Assert.That(await Detalles(cn, p.IdPresupuestoVersion), Is.Empty);
    }

    private static DataTable Lote(params (int Partida, decimal Monto, string? Observacion)[] filas)
    {
        // Mismo orden de columnas que ControlPresupuestario.TVP_PresupuestoDetalleLote.
        var tabla = new DataTable();
        tabla.Columns.Add("IdCatalogoPartida", typeof(int));
        tabla.Columns.Add("MontoPresupuestado", typeof(decimal));
        tabla.Columns.Add("Observacion", typeof(string));
        tabla.Columns.Add("_Fila", typeof(int));
        var n = 1;
        foreach (var f in filas)
            tabla.Rows.Add(f.Partida, f.Monto, (object?)f.Observacion ?? DBNull.Value, n++);
        return tabla;
    }

    private static Task<CargaLoteResultado> CargaLote(SqlConnection cn, int version, DataTable lote,
        bool quitarAusentes = false) =>
        cn.QuerySingleAsync<CargaLoteResultado>(Schema + "usp_PresupuestoDetalle_CargaLote",
            new
            {
                IdPresupuestoVersion = version,
                Detalles = lote.AsTableValuedParameter("ControlPresupuestario.TVP_PresupuestoDetalleLote"),
                QuitarAusentes = quitarAusentes
            }, commandType: CommandType.StoredProcedure);

    private static async Task<Dictionary<int, Detalle>> Detalles(SqlConnection cn, int version) =>
        (await cn.QueryAsync<Detalle>(Schema + "usp_PresupuestoDetalle_ListarPorVersion",
            new { IdPresupuestoVersion = version }, commandType: CommandType.StoredProcedure))
        .ToDictionary(d => d.IdCatalogoPartida);

    private sealed class CargaLoteResultado
    {
        public int Agregados { get; set; }
        public int Actualizados { get; set; }
        public int Eliminados { get; set; }
        public int PartidasEnVersion { get; set; }
        public decimal MontoTotal { get; set; }
    }
}
