using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Cobranzas_Vittoria.Compras.Application.Requerimiento.Crear;
using Cobranzas_Vittoria.Compras.Domain.Excepciones;
using Cobranzas_Vittoria.Compras.Domain.Model;
using Cobranzas_Vittoria.Compras.Domain.Persistence;
using Cobranzas_Vittoria.Compras.Presentation.Dto;
using Cobranzas_Vittoria.Dtos.Compras;
using Cobranzas_Vittoria.Tests.Integration.Common;
using Cobranzas_Vittoria.Tests.Unit.Seguridad.Stubs;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cobranzas_Vittoria.Tests.Integration.Compras;

[NonParallelizable]
public class CrearRequerimientoTests : IntegrationTestBase
{
    private static IRequerimientoRepository Repository => GlobalSetupFixture.Factory.Services.GetRequiredService<IRequerimientoRepository>();
    private static IConsultaCreacionRequerimiento Consultas => GlobalSetupFixture.Factory.Services.GetRequiredService<IConsultaCreacionRequerimiento>();

    [Test]
    public async Task Crear_ContratoActual_ConservaTextosFechasYUsuarioAutenticado()
    {
        CrearRequerimientoRequest request = Request();
        request.NumeroRequerimiento = "  MANUAL  ";
        request.FechaRequerimiento = new DateTime(2026, 11, 10, 17, 30, 0);
        request.FechaEntrega = new DateTime(2026, 11, 9, 13, 0, 0);
        request.IdUsuarioSolicitante = int.MaxValue;
        request.Descripcion = "  Descripción  ";
        request.Observacion = "";
        request.Items![0]!.Observacion = "   ";

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/compras/requerimientos", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        JsonElement body = await response.Content.ReadFromJsonAsync<JsonElement>();
        int id = body.GetProperty("idRequerimiento").GetInt32();
        Requerimiento creado = (await Repository.ObtenerConItemsAsync(id))!;

        Assert.Multiple(() =>
        {
            Assert.That(body.EnumerateObject().Count(), Is.EqualTo(1));
            Assert.That(creado.NumeroRequerimiento, Is.EqualTo("MANUAL"));
            Assert.That(creado.FechaRequerimiento, Is.EqualTo(new DateOnly(2026, 11, 10)));
            Assert.That(creado.FechaEntrega, Is.EqualTo(new DateOnly(2026, 11, 9)));
            Assert.That(creado.IdUsuarioSolicitante, Is.EqualTo(1));
            Assert.That(creado.Estado, Is.EqualTo("Registrado"));
            Assert.That(creado.Descripcion, Is.EqualTo("  Descripción  "));
            Assert.That(creado.Observacion, Is.Empty);
            Assert.That(creado.Items.Single().Observacion, Is.EqualTo("   "));
            Assert.That(creado.Items.Single().IdPresupuestoDetalle, Is.Null);
            Assert.That(creado.FechaCreacion, Is.GreaterThan(DateTime.MinValue));
        });
    }

    [Test]
    public async Task Crear_FechaYNumeroOmitidos_AplicaDefaultsSinVolverElPostIdempotente()
    {
        CrearRequerimientoRequest request = Request();
        request.NumeroRequerimiento = null;
        request.FechaRequerimiento = default;

        int primero = await CrearHttpAsync(request);
        int segundo = await CrearHttpAsync(request);
        Requerimiento a = (await Repository.ObtenerConItemsAsync(primero))!;
        Requerimiento b = (await Repository.ObtenerConItemsAsync(segundo))!;

        Assert.That(primero, Is.Not.EqualTo(segundo));
        Assert.That(a.NumeroRequerimiento, Is.EqualTo("1"));
        Assert.That(b.NumeroRequerimiento, Is.EqualTo("2"));
        Assert.That(a.FechaRequerimiento, Is.EqualTo(DateOnly.FromDateTime(DateTime.Today)));
    }

    [TestCase("sin-items", HttpStatusCode.BadRequest)]
    [TestCase("items-null", HttpStatusCode.BadRequest)]
    [TestCase("item-null", HttpStatusCode.BadRequest)]
    [TestCase("cantidad", HttpStatusCode.BadRequest)]
    [TestCase("precision", HttpStatusCode.BadRequest)]
    [TestCase("duplicado", HttpStatusCode.Conflict)]
    [TestCase("material-ausente", HttpStatusCode.NotFound)]
    [TestCase("proyecto-ausente", HttpStatusCode.NotFound)]
    [TestCase("partida-ausente", HttpStatusCode.Conflict)]
    public async Task Crear_EntradaInvalida_RespondeSinPersistir(string caso, HttpStatusCode esperado)
    {
        CrearRequerimientoRequest request = Request();
        switch (caso)
        {
            case "sin-items": request.Items = []; break;
            case "items-null": request.Items = null; break;
            case "item-null": request.Items = [null]; break;
            case "cantidad": request.Items![0]!.Cantidad = 0m; break;
            case "precision": request.Items![0]!.Cantidad = 1.001m; break;
            case "duplicado": request.Items!.Add(new() { IdMaterial = 2, Cantidad = 2m }); break;
            case "material-ausente": request.Items![0]!.IdMaterial = int.MaxValue; break;
            case "proyecto-ausente": request.IdProyecto = int.MaxValue; break;
            case "partida-ausente": request.Items![0]!.IdPresupuestoDetalle = int.MaxValue; break;
        }

        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/compras/requerimientos", request);
        Assert.That(response.StatusCode, Is.EqualTo(esperado), await response.Content.ReadAsStringAsync());
        Assert.That(await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM compras.Requerimiento"), Is.Zero);
        Assert.That(await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM compras.RequerimientoDetalle"), Is.Zero);
    }

    [TestCase("aprobado")]
    [TestCase("historico")]
    [TestCase("presupuesto-inactivo")]
    [TestCase("centro-inactivo")]
    [TestCase("otro-proyecto")]
    public async Task Crear_Partida_ValidaLaMismaPoliticaEnAplicacionYSql(string caso)
    {
        int partida = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        if (caso != "aprobado") await CambiarPartidaAsync(partida, caso);

        CrearRequerimientoRequest request = Request();
        request.Items![0]!.IdPresupuestoDetalle = partida;
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/compras/requerimientos", request);
        Assert.That(response.StatusCode, Is.EqualTo(caso == "aprobado" ? HttpStatusCode.OK : HttpStatusCode.Conflict),
            await response.Content.ReadAsStringAsync());

        // La escritura directa omite la consulta del handler y debe aplicar la misma defensa.
        Requerimiento directo = Agregado("DIRECTO", partida);
        if (caso == "aprobado")
        {
            int id = await Repository.CrearAsync(directo);
            Assert.That((await Repository.ObtenerConItemsAsync(id))!.Items.Single().IdPresupuestoDetalle, Is.EqualTo(partida));
        }
        else
        {
            ConflictoComprasException? error = Assert.ThrowsAsync<ConflictoComprasException>(() => Repository.CrearAsync(directo));
            Assert.That(error!.CodigoError, Is.EqualTo("PARTIDA_REQUERIMIENTO_INVALIDA"));
            Assert.That(await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM compras.Requerimiento"), Is.Zero);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Crear_AltasConcurrentesConMismoCandidato_ReintentaSinDuplicarNumeros(bool numeroManual)
    {
        TaskCompletionSource barrera = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int lecturas = 0;
        ConsultaInterceptada consulta = new(Consultas, async () =>
        {
            if (Interlocked.Increment(ref lecturas) <= 2)
            {
                if (Volatile.Read(ref lecturas) == 2) barrera.TrySetResult();
                await barrera.Task.WaitAsync(TimeSpan.FromSeconds(20));
            }
        });
        CrearRequerimientoCommand command = Request().ToCommand() with { NumeroRequerimiento = numeroManual ? "MANUAL" : "" };

        CrearRequerimientoResult[] resultados = await Task.WhenAll(
            Handler(consulta).HandleAsync(command), Handler(consulta).HandleAsync(command)).WaitAsync(TimeSpan.FromSeconds(30));

        Assert.That(resultados.Select(r => r.IdRequerimiento).Distinct().Count(), Is.EqualTo(2));
        IEnumerable<string> numeros = await DbHelpers.QueryAsync<string>("SELECT NumeroRequerimiento FROM compras.Requerimiento");
        Assert.That(numeros.Distinct().Count(), Is.EqualTo(2));
        Assert.That(lecturas, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public async Task Crear_EdicionLegacyOcupaElCandidato_RecalculaAntesDeGuardar()
    {
        int existente = await CrearHttpAsync(Request("4"));
        int llamadas = 0;
        ConsultaInterceptada consulta = new(Consultas, async () =>
        {
            if (Interlocked.Increment(ref llamadas) != 1) return;
            HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/compras/requerimientos/{existente}", new RequerimientoUpdateDto
            {
                NumeroRequerimiento = "5", FechaRequerimiento = DateTime.Today, IdProyecto = SeedIds.ProyectoMaytaCapacII,
                Items = [new() { IdMaterial = 2, Cantidad = 1m }]
            });
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        });

        CrearRequerimientoResult creado = await Handler(consulta).HandleAsync(Request("").ToCommand());
        Assert.That((await Repository.ObtenerConItemsAsync(creado.IdRequerimiento))!.NumeroRequerimiento, Is.EqualTo("6"));
        Assert.That(llamadas, Is.EqualTo(2));
    }

    [TestCase("historico")]
    [TestCase("presupuesto-inactivo")]
    [TestCase("centro-inactivo")]
    public async Task Crear_PartidaCambiaTrasValidacion_ElSpRechazaSinGuardar(string caso)
    {
        int partida = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        ConsultaInterceptada consulta = new(Consultas, () => CambiarPartidaAsync(partida, caso));
        CrearRequerimientoRequest request = Request();
        request.Items![0]!.IdPresupuestoDetalle = partida;

        ConflictoComprasException? error = Assert.ThrowsAsync<ConflictoComprasException>(() => Handler(consulta).HandleAsync(request.ToCommand()));
        Assert.That(error!.CodigoError, Is.EqualTo("PARTIDA_REQUERIMIENTO_INVALIDA"));
        Assert.That(await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM compras.Requerimiento"), Is.Zero);
    }

    [Test]
    public async Task Crear_ErrorAlInsertarDetalle_RevierteTambienLaCabecera()
    {
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        await db.ExecuteAsync("""
            CREATE OR ALTER TRIGGER compras.tr_Test_FalloDetalleRequerimiento ON compras.RequerimientoDetalle
            AFTER INSERT AS THROW 51991, 'Fallo de detalle provocado por la prueba.', 1;
            """);
        try
        {
            SqlException? error = Assert.ThrowsAsync<SqlException>(() => Repository.CrearAsync(Agregado("ROLLBACK")));
            Assert.That(error!.Number, Is.EqualTo(51991));
            Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM compras.Requerimiento"), Is.Zero);
            Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM compras.RequerimientoDetalle"), Is.Zero);
        }
        finally
        {
            await db.ExecuteAsync("DROP TRIGGER IF EXISTS compras.tr_Test_FalloDetalleRequerimiento");
        }
    }

    [Test]
    public async Task Crear_TransaccionAbierta_RetieneLaProteccionPresupuestariaHastaElCommit()
    {
        int partida = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        await using SqlConnection escritura = await DbHelpers.OpenTestConnectionAsync();
        using SqlTransaction transaccion = escritura.BeginTransaction();
        using DataTable items = new DataTable();
        items.Columns.Add("IdMaterial", typeof(int));
        items.Columns.Add("Cantidad", typeof(decimal));
        items.Columns.Add("Observacion", typeof(string));
        items.Columns.Add("IdPresupuestoDetalle", typeof(int));
        items.Rows.Add(2, 1.25m, DBNull.Value, partida);

        int id = await escritura.ExecuteScalarAsync<int>("compras.usp_Requerimiento_Crear", new
        {
            NumeroRequerimiento = "PROTEGIDO", FechaRequerimiento = DateTime.Today,
            IdProyecto = SeedIds.ProyectoMaytaCapacII, IdUsuarioSolicitante = 1,
            Items = items.AsTableValuedParameter("compras.TVP_RequerimientoDetalle")
        }, transaccion, commandType: CommandType.StoredProcedure);

        await using SqlConnection cambio = await DbHelpers.OpenTestConnectionAsync();
        await cambio.ExecuteAsync("SET LOCK_TIMEOUT 300");
        const string sqlCambio = """
            UPDATE p SET Activo=0 FROM ControlPresupuestario.Presupuesto p
            JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuesto=p.IdPresupuesto
            JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoVersion=pv.IdPresupuestoVersion
            WHERE pd.IdPresupuestoDetalle=@partida;
            """;

        SqlException? error = Assert.ThrowsAsync<SqlException>(() => cambio.ExecuteAsync(sqlCambio, new { partida }));
        Assert.That(error!.Number, Is.EqualTo(1222), "El presupuesto debe seguir protegido hasta confirmar el requerimiento.");

        transaccion.Commit();
        await cambio.ExecuteAsync(sqlCambio, new { partida });
        Assert.That((await Repository.ObtenerConItemsAsync(id))!.Items.Single().IdPresupuestoDetalle, Is.EqualTo(partida));
    }

    [Test]
    public async Task Crear_SpReaplicable_ConservaParametrosYTipoTabular()
    {
        Assembly assembly = typeof(Program).Assembly;
        string nombre = assembly.GetManifestResourceNames().Single(n => n.EndsWith("R__Compras_SPs.sql"));
        using StreamReader reader = new(assembly.GetManifestResourceStream(nombre)!);
        string script = await reader.ReadToEndAsync();
        int inicio = script.IndexOf("CREATE OR ALTER PROCEDURE [compras].[usp_Requerimiento_Crear]", StringComparison.Ordinal);
        string sp = script[inicio..script.IndexOf("\nGO", inicio, StringComparison.Ordinal)];
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        await db.ExecuteAsync(sp);
        await db.ExecuteAsync(sp);
        Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.parameters WHERE object_id=OBJECT_ID('compras.usp_Requerimiento_Crear')"), Is.EqualTo(8));
        Assert.That(await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sys.columns WHERE object_id=(SELECT type_table_object_id FROM sys.table_types WHERE user_type_id=TYPE_ID('compras.TVP_RequerimientoDetalle'))"), Is.EqualTo(4));
        Assert.That(await CrearHttpAsync(Request()), Is.GreaterThan(0));
    }

    private async Task<int> CrearHttpAsync(CrearRequerimientoRequest request)
    {
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/compras/requerimientos", request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("idRequerimiento").GetInt32();
    }

    private static CrearRequerimientoRequest Request(string numero = "MANUAL") => new()
    {
        NumeroRequerimiento = numero, FechaRequerimiento = DateTime.Today, IdProyecto = SeedIds.ProyectoMaytaCapacII,
        Items = [new() { IdMaterial = 2, Cantidad = 1.25m }]
    };

    private static Requerimiento Agregado(string numero, int? partida = null) => Requerimiento.Crear(numero,
        DateOnly.FromDateTime(DateTime.Today), SeedIds.ProyectoMaytaCapacII, 1,
        [ItemRequerimiento.CrearParaNuevoRequerimiento(2, partida, 1.25m, null)]);

    private static CrearRequerimientoHandler Handler(IConsultaCreacionRequerimiento consultas) => new(Repository, consultas,
        new StubUsuarioActualService(), TimeProvider.System, NullLogger<CrearRequerimientoHandler>.Instance);

    private static async Task CambiarPartidaAsync(int partida, string caso)
    {
        string sql = caso switch
        {
            "historico" => """
                UPDATE pv SET IdEstadoPresupuesto=(SELECT IdEstadoPresupuesto FROM ControlPresupuestario.EstadoPresupuesto WHERE Codigo='HISTORICO')
                FROM ControlPresupuestario.PresupuestoVersion pv JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoVersion=pv.IdPresupuestoVersion
                WHERE pd.IdPresupuestoDetalle=@partida
                """,
            "presupuesto-inactivo" => """
                UPDATE p SET Activo=0 FROM ControlPresupuestario.Presupuesto p
                JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuesto=p.IdPresupuesto
                JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoVersion=pv.IdPresupuestoVersion WHERE pd.IdPresupuestoDetalle=@partida
                """,
            _ => """
                UPDATE cc SET Activo=CASE WHEN @caso='centro-inactivo' THEN 0 ELSE cc.Activo END,
                    IdProyecto=CASE WHEN @caso='otro-proyecto' THEN NULL ELSE cc.IdProyecto END
                FROM ControlPresupuestario.CentroCosto cc JOIN ControlPresupuestario.Presupuesto p ON p.IdCentroCosto=cc.IdCentroCosto
                JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuesto=p.IdPresupuesto
                JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoVersion=pv.IdPresupuestoVersion WHERE pd.IdPresupuestoDetalle=@partida
                """
        };
        await using SqlConnection db = await DbHelpers.OpenTestConnectionAsync();
        await db.ExecuteAsync(sql, new { partida, caso });
    }

    private sealed class ConsultaInterceptada(IConsultaCreacionRequerimiento original, Func<Task> despuesDeNumeracion) : IConsultaCreacionRequerimiento
    {
        public Task<ContextoCreacionRequerimiento> ObtenerContextoAsync(int idProyecto, int idSolicitante,
            IReadOnlyCollection<int> materiales, IReadOnlyCollection<int> partidas, CancellationToken ct)
            => original.ObtenerContextoAsync(idProyecto, idSolicitante, materiales, partidas, ct);

        public async Task<DisponibilidadNumeroRequerimiento> ObtenerNumeracionAsync(string numeroSolicitado, CancellationToken ct)
        {
            DisponibilidadNumeroRequerimiento resultado = await original.ObtenerNumeracionAsync(numeroSolicitado, ct);
            await despuesDeNumeracion();
            return resultado;
        }
    }
}
