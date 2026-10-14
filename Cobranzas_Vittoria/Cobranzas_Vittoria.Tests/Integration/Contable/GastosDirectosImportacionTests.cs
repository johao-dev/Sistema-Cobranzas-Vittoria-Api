using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Importar;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Cobranzas_Vittoria.Tests.Integration.Common;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Microsoft.Extensions.DependencyInjection;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Model;
using Cobranzas_Vittoria.Contable.GastosDirectos.Domain.Persistence;

namespace Cobranzas_Vittoria.Tests.Integration.Contable;

public sealed class GastosDirectosImportacionTests : IntegrationTestBase
{
    private const string Ruta = "/api/contable/gastos-directos/importar";
    private Contexto _datos = null!;

    private sealed class Contexto
    {
        public int Detalle { get; set; }
        public int Centro { get; set; }
        public int CatalogoPartida { get; set; }
        public int Moneda { get; set; }
        public string CodigoCentro { get; set; } = "";
        public string CodigoPartida { get; set; } = "";
        public int Proveedor { get; set; }
        public string Ruc { get; set; } = "";
        public string RazonSocial { get; set; } = "";
    }

    [SetUp]
    public async Task Preparar()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            JwtTestTokenFactory.CrearToken(permisos: new[] { Permisos.GastoDirecto.Ver, Permisos.GastoDirecto.Operar }));
        var detalle = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync();
        _datos = (await DbHelpers.QueryAsync<Contexto>("""
            SELECT pd.IdPresupuestoDetalle AS Detalle, cc.IdCentroCosto AS Centro,
                cp.IdCatalogoPartida AS CatalogoPartida, p.IdMoneda AS Moneda,
                cc.Codigo AS CodigoCentro, cp.Codigo AS CodigoPartida
            FROM ControlPresupuestario.PresupuestoDetalle pd
            JOIN ControlPresupuestario.CatalogoPartida cp ON cp.IdCatalogoPartida=pd.IdCatalogoPartida
            JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuestoVersion=pd.IdPresupuestoVersion
            JOIN ControlPresupuestario.Presupuesto p ON p.IdPresupuesto=pv.IdPresupuesto
            JOIN ControlPresupuestario.CentroCosto cc ON cc.IdCentroCosto=p.IdCentroCosto
            WHERE pd.IdPresupuestoDetalle=@detalle
            """, new { detalle })).Single();
        _datos.Ruc = "20" + Random.Shared.Next(100000000, 999999999);
        _datos.RazonSocial = "IMPORTACIÓN " + Guid.NewGuid().ToString("N");
        _datos.Proveedor = await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO maestra.Proveedor (RazonSocial, Ruc, Activo) VALUES (@nombre, @ruc, 1);
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { nombre = _datos.RazonSocial, ruc = _datos.Ruc });
    }

    [TestCase("OTROS")]
    [TestCase("MARKETING_VENTAS")]
    [TestCase("TERRENO")]
    [TestCase("ANTEPROYECTO")]
    [TestCase("PROYECTO")]
    [TestCase("ADMINISTRATIVO")]
    [TestCase("MUNICIPAL")]
    public async Task ImportarCategoria_UsaCatalogosYAuditoriaDelAltaManual(string categoria)
    {
        using var libro = Libro();
        Fila(libro, 6, categoria);
        var inicio = DateTime.UtcNow.AddSeconds(-2);
        var r = await Importar(libro);
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK), await r.Content.ReadAsStringAsync());
        var resumen = await Json(r);
        Assert.Multiple(() =>
        {
            Assert.That(resumen.GetProperty("filasInsertadas").GetInt32(), Is.EqualTo(1));
            Assert.That(resumen.GetProperty("totalFilas").GetInt32(), Is.EqualTo(1));
            Assert.That(resumen.GetProperty("filasValidas").GetInt32(), Is.EqualTo(1));
            Assert.That(resumen.GetProperty("filasInvalidas").GetInt32(), Is.Zero);
        });
        var gasto = (await _client.GetFromJsonAsync<JsonElement>("/api/contable/gastos-directos")).EnumerateArray().Single();
        Assert.Multiple(() =>
        {
            Assert.That(gasto.GetProperty("codigoCategoriaGasto").GetString(), Is.EqualTo(categoria));
            Assert.That(gasto.GetProperty("idPresupuestoDetalle").GetInt32(), Is.EqualTo(_datos.Detalle));
            Assert.That(gasto.GetProperty("idProveedor").GetInt32(), Is.EqualTo(_datos.Proveedor));
            Assert.That(gasto.GetProperty("idMoneda").GetInt32(), Is.EqualTo(_datos.Moneda));
            Assert.That(gasto.GetProperty("estado").GetString(), Is.EqualTo("REGISTRADO"));
            Assert.That(gasto.GetProperty("concepto").GetString(), Is.EqualTo("Gasto importado"));
            Assert.That(gasto.GetProperty("descripcion").GetString(), Is.EqualTo("Descripción"));
            Assert.That(gasto.GetProperty("monto").GetDecimal(), Is.EqualTo(1880m));
            Assert.That(gasto.GetProperty("usuarioCreacion").GetString(), Is.EqualTo(JwtTestTokenFactory.UsuarioLogin));
            Assert.That(gasto.GetProperty("fechaCreacion").GetDateTime(), Is.GreaterThanOrEqualTo(inicio));
            Assert.That(gasto.GetProperty("fechaActualizacion").ValueKind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(gasto.GetProperty("idMonedaOriginal").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });
    }

    [TestCase("F001-245")]
    [TestCase("F001-245; INV-10025")]
    public async Task FacturaUsd_ConservaMontoYAsociaReferenciasEnRelacionExistente(string facturas)
    {
        using var libro = Libro();
        Fila(libro, 6);
        Celda(libro, 6, "Moneda factura", "USD");
        Celda(libro, 6, "Monto factura", 500d);
        Celda(libro, 6, "Tipo de cambio", 3.76d);
        Celda(libro, 6, "Fecha tipo cambio", new DateTime(2026, 10, 5));
        Celda(libro, 6, "Factura(s)", facturas);
        var r = await Importar(libro);
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK), await r.Content.ReadAsStringAsync());
        var gasto = (await _client.GetFromJsonAsync<JsonElement>("/api/contable/gastos-directos")).EnumerateArray().Single();
        Assert.That(gasto.GetProperty("monto").GetDecimal(), Is.EqualTo(1880m));
        Assert.That(gasto.GetProperty("monedaOriginal").GetString(), Is.EqualTo("USD"));
        Assert.That(gasto.GetProperty("montoOriginal").GetDecimal(), Is.EqualTo(500m));
        Assert.That(gasto.GetProperty("tipoCambio").GetDecimal(), Is.EqualTo(3.76m));
        var id = gasto.GetProperty("idGastoDirecto").GetInt32();
        var documentos = (await _client.GetFromJsonAsync<JsonElement>($"/api/contable/gastos-directos/{id}/documentos")).EnumerateArray().ToArray();
        Assert.That(documentos.Select(d => d.GetProperty("numeroFactura").GetString()),
            Is.EquivalentTo(facturas.Split(';', StringSplitOptions.TrimEntries)));
        Assert.That(documentos.All(d => d.GetProperty("tipoDocumento").GetString() == "Factura" && !d.GetProperty("tieneArchivo").GetBoolean()), Is.True);
        var doc = documentos[0].GetProperty("idGastoDirectoDocumento").GetInt32();
        Assert.That((await _client.GetAsync($"/api/contable/gastos-directos/{id}/documentos/{doc}/download")).StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    [TestCase("Categoría código", "VENTAS")]
    [TestCase("Categoría código", "26")]
    [TestCase("Categoría código", " ")]
    [TestCase("Centro de costo", " ")]
    [TestCase("Partida", " ")]
    [TestCase("Fecha", " ")]
    [TestCase("Concepto", " ")]
    [TestCase("Proveedor", " ")]
    [TestCase("Monto", " ")]
    [TestCase("Monto", "0")]
    [TestCase("Monto", "-1")]
    [TestCase("Monto", "1.234")]
    [TestCase("Monto", "1,234")]
    [TestCase("Monto", "10000000000000000")]
    [TestCase("Monto", "abc")]
    [TestCase("Fecha", "31/02/2026")]
    [TestCase("Fecha", "10/14/2026")]
    [TestCase("Fecha", "01/02/26")]
    [TestCase("Centro de costo", "NO-EXISTE")]
    [TestCase("Partida", "NO-EXISTE")]
    [TestCase("Proveedor", "NO-EXISTE")]
    public async Task FilaInvalida_ReportaFilaRealYRevierteTodoElLote(string campo, string valor)
    {
        using var libro = Libro();
        Fila(libro, 6);
        Celda(libro, 6, "Factura(s)", "F001-1");
        Fila(libro, 8);
        Celda(libro, 8, campo, valor);
        var r = await Importar(libro);
        await Rechazado(r, campo, 8, 2, 1);
    }

    [TestCase("Moneda factura", "EUR")]
    [TestCase("Tipo de cambio", "0")]
    [TestCase("Tipo de cambio", "-1")]
    [TestCase("Tipo de cambio", "abc")]
    [TestCase("Monto factura", "-1")]
    [TestCase("Fecha tipo cambio", "31/02/2026")]
    public async Task BloqueExtranjero_ValidaCadaCampo(string campo, string valor)
    {
        using var libro = Libro();
        Fila(libro, 6);
        Celda(libro, 6, "Moneda factura", "USD");
        Celda(libro, 6, "Monto factura", "10");
        Celda(libro, 6, "Tipo de cambio", "3.76");
        Celda(libro, 6, "Fecha tipo cambio", "05/10/2026");
        Celda(libro, 6, campo, valor);
        await Rechazado(await Importar(libro), campo);
    }

    [TestCase("Moneda factura")]
    [TestCase("Monto factura")]
    [TestCase("Tipo de cambio")]
    [TestCase("Fecha tipo cambio")]
    public async Task BloqueExtranjero_ExigeLosCuatroCampos(string ausente)
    {
        using var libro = Libro();
        Fila(libro, 6);
        foreach (var (campo, valor) in new[] { ("Moneda factura", "USD"), ("Monto factura", "10"),
                     ("Tipo de cambio", "3.76"), ("Fecha tipo cambio", "05/10/2026") })
            Celda(libro, 6, campo, campo == ausente ? "" : valor);
        await Rechazado(await Importar(libro), ausente);
    }

    [Test]
    public async Task CategoriaInactiva_Rechaza()
    {
        await DbHelpers.QueryScalarAsync<int>("UPDATE maestra.CategoriaGasto SET Activo=0 WHERE Codigo='OTROS'; SELECT 1;");
        try
        {
            using var libro = Libro();
            Fila(libro, 6);
            await Rechazado(await Importar(libro), "Categoría código");
        }
        finally { await DbHelpers.QueryScalarAsync<int>("UPDATE maestra.CategoriaGasto SET Activo=1 WHERE Codigo='OTROS'; SELECT 1;"); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ProveedorInactivoOAmbiguo_Rechaza(bool ambiguo)
    {
        if (ambiguo)
            await DbHelpers.QueryScalarAsync<int>("INSERT INTO maestra.Proveedor (RazonSocial, Activo) VALUES (@nombre,1); SELECT 1;", new { nombre = _datos.RazonSocial });
        else
            await DbHelpers.QueryScalarAsync<int>("UPDATE maestra.Proveedor SET Activo=0 WHERE IdProveedor=@id; SELECT 1;", new { id = _datos.Proveedor });
        using var libro = Libro();
        Fila(libro, 6);
        Celda(libro, 6, "Proveedor", _datos.RazonSocial);
        await Rechazado(await Importar(libro), "Proveedor");
    }

    [Test]
    public async Task PartidaDeOtroCentro_Rechaza()
    {
        var proyecto = await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO maestra.Proyecto (NombreProyecto, Activo) VALUES (@nombre, 1);
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { nombre = "OTRO CENTRO " + Guid.NewGuid().ToString("N") });
        var otro = await IntegracionEconomicaTestData.ObtenerOCrearDetalleAprobadoAsync(proyecto);
        var codigo = await DbHelpers.QueryScalarAsync<string>("""
            SELECT cp.Codigo FROM ControlPresupuestario.PresupuestoDetalle pd
            JOIN ControlPresupuestario.CatalogoPartida cp ON cp.IdCatalogoPartida=pd.IdCatalogoPartida
            WHERE pd.IdPresupuestoDetalle=@otro
            """, new { otro });
        using var libro = Libro();
        Fila(libro, 6);
        Celda(libro, 6, "Partida", codigo!);
        await Rechazado(await Importar(libro), "Partida");
    }

    [TestCase("Centro")]
    [TestCase("Partida")]
    [TestCase("Presupuesto")]
    [TestCase("Version")]
    public async Task RelacionesNoHabilitadas_Rechaza(string entidad)
    {
        var sql = entidad switch
        {
            "Centro" => "UPDATE ControlPresupuestario.CentroCosto SET Activo=0 WHERE IdCentroCosto=@Centro;",
            "Partida" => "UPDATE ControlPresupuestario.CatalogoPartida SET Activo=0 WHERE IdCatalogoPartida=@CatalogoPartida;",
            "Presupuesto" => "UPDATE p SET Activo=0 FROM ControlPresupuestario.Presupuesto p JOIN ControlPresupuestario.PresupuestoVersion pv ON pv.IdPresupuesto=p.IdPresupuesto JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoVersion=pv.IdPresupuestoVersion WHERE pd.IdPresupuestoDetalle=@Detalle;",
            _ => "UPDATE pv SET IdEstadoPresupuesto=(SELECT IdEstadoPresupuesto FROM ControlPresupuestario.EstadoPresupuesto WHERE Codigo='BORRADOR') FROM ControlPresupuestario.PresupuestoVersion pv JOIN ControlPresupuestario.PresupuestoDetalle pd ON pd.IdPresupuestoVersion=pv.IdPresupuestoVersion WHERE pd.IdPresupuestoDetalle=@Detalle;"
        };
        await DbHelpers.QueryScalarAsync<int>(sql + " SELECT 1;", _datos);
        using var libro = Libro();
        Fila(libro, 6);
        await Rechazado(await Importar(libro), entidad == "Centro" ? "Centro de costo" : "Partida");
    }

    [TestCase("alterado")]
    [TestCase("ausente")]
    [TestCase("desordenado")]
    public async Task EncabezadosInexactos_RechazaEstructura(string caso)
    {
        using var libro = Libro();
        var fila = libro.GetSheet(PlantillaGastosDirectos.Hoja).GetRow(4);
        fila.GetCell(1).SetCellValue(caso == "ausente" ? "" : caso == "desordenado" ? "Centro de costo" : "Categoria codigo");
        if (caso == "desordenado") fila.GetCell(2).SetCellValue("Categoría código");
        Fila(libro, 6);
        var r = await Importar(libro);
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await r.Content.ReadAsStringAsync());
        await NadaPersistido();
    }

    [TestCase(0, HttpStatusCode.UnprocessableEntity)]
    [TestCase(200, HttpStatusCode.OK)]
    [TestCase(201, HttpStatusCode.UnprocessableEntity)]
    public async Task LimitesDeRegistros_SeCuentanSinAuxiliares(int cantidad, HttpStatusCode esperado)
    {
        using var libro = Libro();
        for (var i = 0; i < cantidad; i++) Fila(libro, 6 + i);
        var r = await Importar(libro);
        Assert.That(r.StatusCode, Is.EqualTo(esperado), await r.Content.ReadAsStringAsync());
        var resumen = await Json(r);
        Assert.That(resumen.GetProperty("totalFilas").GetInt32(), Is.EqualTo(cantidad));
        if (esperado != HttpStatusCode.OK) await NadaPersistido();
    }

    [Test]
    public async Task FilasVaciasEntreRegistrosYEnOtrasHojas_SeIgnoran()
    {
        using var libro = Libro();
        Fila(libro, 6);
        Fila(libro, 9);
        Celda(libro, 7, "Registro", "999");
        Celda(libro, 7, "Estado de fila", "LISTO");
        Celda(libro, 7, "Observaciones de validación", "No confiar");
        var r = await Importar(libro);
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK), await r.Content.ReadAsStringAsync());
        Assert.That((await Json(r)).GetProperty("totalFilas").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public async Task FallaAlAsociarSegundaFactura_RollbackIncluyeGastosYDocumentosPrevios()
    {
        await DbHelpers.QueryScalarAsync<int>("""
            EXEC(N'CREATE TRIGGER contable.tr_TestImportacionFactura ON contable.GastoDirectoDocumento AFTER INSERT AS
                BEGIN IF EXISTS (SELECT 1 FROM inserted WHERE NombreArchivo=''F-FAIL'')
                    THROW 51511, ''FACTURA_INVALIDA: fallo de prueba al asociar factura.'', 1; END;');
            SELECT 1;
            """);
        try
        {
            using var libro = Libro();
            Fila(libro, 6);
            Fila(libro, 8);
            Celda(libro, 6, "Factura(s)", "F001-1;F001-2");
            Celda(libro, 8, "Factura(s)", "F-FAIL");
            await Rechazado(await Importar(libro), "Factura(s)", 8, 2, 1);
        }
        finally { await DbHelpers.QueryScalarAsync<int>("DROP TRIGGER IF EXISTS contable.tr_TestImportacionFactura; SELECT 1;"); }
    }

    [TestCase(null, HttpStatusCode.Unauthorized)]
    [TestCase(Permisos.GastoDirecto.Ver, HttpStatusCode.Forbidden)]
    public async Task Importar_ExigeMismoPermisoDeCreacion(string? permiso, HttpStatusCode esperado)
    {
        _client.DefaultRequestHeaders.Authorization = permiso is null ? null : new AuthenticationHeaderValue("Bearer",
            JwtTestTokenFactory.CrearToken(permisos: new[] { permiso }));
        using var libro = Libro();
        Fila(libro, 6);
        Assert.That((await Importar(libro)).StatusCode, Is.EqualTo(esperado));
        await NadaPersistido();
    }

    [Test]
    public async Task DescargarPlantilla_EntregaElContratoOriginal()
    {
        var r = await _client.GetAsync("/api/contable/gastos-directos/plantilla");
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await r.Content.ReadAsByteArrayAsync(), Is.EqualTo(new ObtenerPlantillaGastosDirectosHandler().Handle()));
    }

    [Test]
    public async Task RevalidacionTransaccional_RechazaRelacionDesactivadaTrasLeerArchivo()
    {
        var categoria = await DbHelpers.QueryScalarAsync<int>("SELECT IdCategoriaGasto FROM maestra.CategoriaGasto WHERE Codigo='OTROS';");
        var registro = RegistroGastoDirecto.Crear(_datos.Detalle, categoria, _datos.Proveedor, _datos.Moneda,
            new DateTime(2026, 10, 5), "Gasto", null, 10m, null, null, null, null);
        await DbHelpers.QueryScalarAsync<int>("UPDATE maestra.Proveedor SET Activo=0 WHERE IdProveedor=@id; SELECT 1;", new { id = _datos.Proveedor });
        using var scope = GlobalSetupFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGastoDirectoRepository>();
        var error = Assert.ThrowsAsync<DatosInvalidosException>(() => repository.CrearLoteAsync(
            new[] { new GastoDirectoImportado(8, _datos.Centro, registro, new[] { "F001-1" }) }, "test"));
        Assert.That(error!.Errores.Single().Fila, Is.EqualTo(8));
        Assert.That(error.Errores.Single().Campo, Is.EqualTo("Proveedor"));
        await NadaPersistido();
    }

    [Test]
    public async Task CamposConFormulaVacia_SeValidanComoObligatorios()
    {
        using var libro = Libro();
        Fila(libro, 6);
        var celda = libro.GetSheet(PlantillaGastosDirectos.Hoja).GetRow(5).GetCell(5);
        celda.SetCellFormula("IF(1=1,\"\",\"Concepto\")");
        libro.GetCreationHelper().CreateFormulaEvaluator().EvaluateFormulaCell(celda);
        await Rechazado(await Importar(libro), "Concepto");
    }

    [TestCase("Concepto", 251)]
    [TestCase("Descripción", 501)]
    [TestCase("Factura(s)", 256)]
    public async Task LongitudesDeSql_SeValidanAntesDePersistir(string campo, int longitud)
    {
        using var libro = Libro();
        Fila(libro, 6);
        Celda(libro, 6, campo, new string('a', longitud));
        await Rechazado(await Importar(libro), campo);
    }

    [Test]
    public async Task ProveedorPorRazonSocialYFechaTexto_SeResuelvenComoElFormulario()
    {
        using var libro = Libro();
        Fila(libro, 6);
        Celda(libro, 6, "Proveedor", _datos.RazonSocial);
        Celda(libro, 6, "Fecha", "05/10/2026");
        Celda(libro, 6, "Descripción", " ");
        var r = await Importar(libro);
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK), await r.Content.ReadAsStringAsync());
        var gasto = (await _client.GetFromJsonAsync<JsonElement>("/api/contable/gastos-directos")).EnumerateArray().Single();
        Assert.That(gasto.GetProperty("idProveedor").GetInt32(), Is.EqualTo(_datos.Proveedor));
        Assert.That(gasto.GetProperty("descripcion").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    private static XSSFWorkbook Libro() => new(new MemoryStream(new ObtenerPlantillaGastosDirectosHandler().Handle()));

    private void Fila(XSSFWorkbook libro, int numero, string categoria = "OTROS")
    {
        foreach (var (campo, valor) in new[] { ("Categoría código", categoria), ("Centro de costo", _datos.CodigoCentro),
                     ("Partida", _datos.CodigoPartida), ("Concepto", "  Gasto importado  "), ("Proveedor", _datos.Ruc),
                     ("Descripción", "  Descripción  "), ("Estado de fila", "LISTO") })
            Celda(libro, numero, campo, valor);
        Celda(libro, numero, "Fecha", new DateTime(2026, 10, 5));
        Celda(libro, numero, "Monto", 1880d);
    }

    internal static void Celda(XSSFWorkbook libro, int numero, string campo, object valor)
    {
        var sheet = libro.GetSheet(PlantillaGastosDirectos.Hoja);
        var row = sheet.GetRow(numero - 1) ?? sheet.CreateRow(numero - 1);
        var columna = PlantillaGastosDirectos.Encabezados.ToList().IndexOf(campo);
        if (row.GetCell(columna) is { } anterior) row.RemoveCell(anterior);
        var celda = row.CreateCell(columna);
        switch (valor)
        {
            case DateTime fecha:
                celda.SetCellValue(fecha);
                var estilo = libro.CreateCellStyle();
                estilo.DataFormat = libro.CreateDataFormat().GetFormat("dd/mm/yyyy");
                celda.CellStyle = estilo;
                break;
            case double numeroValor: celda.SetCellValue(numeroValor); break;
            default: celda.SetCellValue((string)valor); break;
        }
    }

    private async Task<HttpResponseMessage> Importar(XSSFWorkbook libro)
    {
        using var stream = new MemoryStream();
        libro.Write(stream, true);
        using var body = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(stream.ToArray());
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        body.Add(archivo, "archivo", "gastos.xlsx");
        // La API debe ignorar cualquier identidad enviada por el formulario.
        body.Add(new StringContent("usuario-falso"), "usuario");
        return await _client.PostAsync(Ruta, body);
    }

    private async Task Rechazado(HttpResponseMessage r, string campo, int fila = 6, int total = 1, int validas = 0)
    {
        Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity), await r.Content.ReadAsStringAsync());
        var json = await Json(r);
        Assert.Multiple(() =>
        {
            Assert.That(json.GetProperty("filasInsertadas").GetInt32(), Is.Zero);
            Assert.That(json.GetProperty("totalFilas").GetInt32(), Is.EqualTo(total));
            Assert.That(json.GetProperty("filasValidas").GetInt32(), Is.EqualTo(validas));
            Assert.That(json.GetProperty("filasInvalidas").GetInt32(), Is.EqualTo(total - validas));
            Assert.That(json.GetProperty("errores").EnumerateArray().Any(e => e.GetProperty("fila").GetInt32() == fila && e.GetProperty("campo").GetString() == campo), Is.True);
        });
        await NadaPersistido();
    }

    private static Task<JsonElement> Json(HttpResponseMessage r) => r.Content.ReadFromJsonAsync<JsonElement>();

    private static async Task NadaPersistido()
    {
        Assert.That(await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM contable.GastoDirecto"), Is.Zero);
        Assert.That(await DbHelpers.QueryScalarAsync<int>("SELECT COUNT(*) FROM contable.GastoDirectoDocumento"), Is.Zero);
    }
}
