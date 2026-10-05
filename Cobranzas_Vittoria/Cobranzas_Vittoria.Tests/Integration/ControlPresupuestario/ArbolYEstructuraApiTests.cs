using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cobranzas_Vittoria.Tests.Integration.Common;
using CP = Cobranzas_Vittoria.Seguridad.Authorization.Permisos.ControlPresupuestario;

namespace Cobranzas_Vittoria.Tests.Integration.ControlPresupuestario;

/// <summary>
/// Árbol de partidas con subtotales y carga de un presupuesto jerárquico (categorías y hojas) desde
/// Excel/CSV: la importación crea las partidas que faltan y carga los montos en una sola transacción.
/// </summary>
[NonParallelizable]
public sealed class ArbolYEstructuraApiTests : IntegrationTestBase
{
    private const string Base = "/api/control-presupuestario";

    private static readonly string[] Permisos =
    {
        CP.CentroCosto.Crear, CP.Partida.Ver, CP.Partida.Crear, CP.Presupuesto.Ver, CP.Presupuesto.Crear,
        CP.Presupuesto.EditarDetalle, CP.Presupuesto.RegistrarAjuste, CP.Version.Aprobar, CP.Reporte.Ver
    };

    // Formato de las áreas: código en columnas de nivel, subtítulos sin monto y totales con decimales de fórmula.
    private const string PresupuestoPorNiveles = """
        Nivel 1;Nivel 2;Nivel 3;Descripción;Und.;Metrado;Precio;Total;Tipo;Seccion
        1;0;0;GASTOS DIRECTOS;;;;;MATERIALES;
        1;1;0;ESTRUCTURAS;;;;;;
        1;1;1;CONCRETO;m3;10;100;1000.004;;
        1;1;2;ACERO;kg;5;100;500;;
        1;2;0;ARQUITECTURA;;;;0;;
        1;2;1;TARRAJEO;m2;1;250;250;;
        2;0;0;GASTOS INDIRECTOS;;;;;INDIRECTOS;
        2;1;0;LICENCIA;glb;1;300;300;;
        """;

    private int _idPen;

    [SetUp]
    public async Task Preparar()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", JwtTestTokenFactory.CrearToken(permisos: Permisos));
        _idPen = await DbHelpersMoneda.ObtenerPenAsync();
        await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO ControlPresupuestario.EstadoPresupuesto (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('BORRADOR'), ('APROBADO'), ('HISTORICO'), ('ANULADO')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.EstadoPresupuesto e WHERE e.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.TipoMovimientoPresupuestal (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('COMPROMISO'), ('LIBERACION'), ('EJECUCION'), ('AJUSTE')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoMovimientoPresupuestal t WHERE t.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.TipoCentroCosto (Codigo, Nombre)
            SELECT Codigo, Codigo FROM (VALUES ('ADMINISTRACION')) v(Codigo)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoCentroCosto t WHERE t.Codigo = v.Codigo);
            INSERT INTO ControlPresupuestario.TipoPartida (Codigo, Nombre)
            SELECT Codigo, Nombre FROM (VALUES ('MATERIALES', 'Materiales'), ('INDIRECTOS', 'Costos indirectos')) v(Codigo, Nombre)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoPartida t WHERE t.Codigo = v.Codigo);
            SELECT 1;
            """);
    }

    [Test]
    public async Task ImportarEstructura_CreaCategoriasYHojas_YElArbolDeLaVersionSumaSubtotales()
    {
        var (idPresupuesto, idVersion, _) = await CrearPresupuestoAsync();
        var ruta = $"{Base}/presupuestos/{idPresupuesto}/versiones/{idVersion}";

        var importar = await SubirAsync($"{ruta}/partidas/importar-estructura", PresupuestoPorNiveles);

        Assert.That(importar.StatusCode, Is.EqualTo(HttpStatusCode.OK), await importar.Content.ReadAsStringAsync());
        var r = await Json(importar);
        Assert.That(r.GetProperty("partidasCreadas").GetInt32(), Is.EqualTo(8));
        Assert.That(r.GetProperty("partidasEnVersion").GetInt32(), Is.EqualTo(4));
        Assert.That(r.GetProperty("montoTotal").GetDecimal(), Is.EqualTo(2050m));
        Assert.That(await DbHelpers.QueryScalarAsync<string>("""
            SELECT t.Codigo FROM ControlPresupuestario.CatalogoPartida p
            JOIN ControlPresupuestario.TipoPartida t ON t.IdTipoPartida = p.IdTipoPartida WHERE p.Codigo = '1.1.2'
            """), Is.EqualTo("MATERIALES"), "Las hijas heredan el tipo de su categoría raíz.");

        var arbol = await _client.GetFromJsonAsync<JsonElement>($"{ruta}/arbol");
        var nodos = arbol.GetProperty("nodos").EnumerateArray().ToDictionary(n => n.GetProperty("codigo").GetString()!);
        Assert.That(nodos.Keys, Is.EqualTo(new[] { "1", "1.1", "1.1.1", "1.1.2", "1.2", "1.2.1", "2", "2.1" }));
        Assert.That(nodos["1.1"].GetProperty("montoPresupuestado").GetDecimal(), Is.EqualTo(1500m));
        Assert.That(nodos["1"].GetProperty("montoPresupuestado").GetDecimal(), Is.EqualTo(1750m));
        Assert.That(nodos["1"].GetProperty("cantidadHojas").GetInt32(), Is.EqualTo(3));
        Assert.That(nodos["1.1.1"].GetProperty("montoPresupuestado").GetDecimal(), Is.EqualTo(1000m));
        Assert.That(nodos["1.1.1"].GetProperty("idPresupuestoDetalle").ValueKind, Is.EqualTo(JsonValueKind.Number));
        Assert.That(nodos["1.1"].GetProperty("idPresupuestoDetalle").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(arbol.GetProperty("totales").GetProperty("montoPresupuestado").GetDecimal(), Is.EqualTo(2050m));
        Assert.That(arbol.GetProperty("encabezado").GetProperty("estadoPresupuesto").GetString(), Is.EqualTo("BORRADOR"));

        // Volver a subir el mismo archivo reutiliza las partidas: no crea nada y actualiza los montos.
        var repetir = await Json(await SubirAsync($"{ruta}/partidas/importar-estructura", PresupuestoPorNiveles));
        Assert.That(repetir.GetProperty("partidasCreadas").GetInt32(), Is.Zero);
        Assert.That(repetir.GetProperty("actualizados").GetInt32(), Is.EqualTo(4));
    }

    [Test]
    public async Task ImportarEstructura_ConErrores_Devuelve422PorFilaYNoCreaNada()
    {
        var (idPresupuesto, idVersion, _) = await CrearPresupuestoAsync();
        const string conErrores = """
            Codigo;Nombre;Monto;Tipo
            9;RAIZ;100;MATERIALES
            9.1;HOJA;50;
            9.1;REPETIDA;1;
            7.1;HUERFANA;1;
            8;SIN TIPO;1;
            6;TIPO RARO;1;NO_EXISTE
            """;

        var respuesta = await SubirAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{idVersion}/partidas/importar-estructura", conErrores);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity), await respuesta.Content.ReadAsStringAsync());
        var errores = (await Json(respuesta)).GetProperty("errores").EnumerateArray()
            .Select(e => (e.GetProperty("fila").GetInt32(), e.GetProperty("codigoError").GetString()))
            .ToList();
        Assert.That(errores, Does.Contain((1, "PADRE_CON_MONTO")));
        Assert.That(errores, Does.Contain((3, "VALOR_DUPLICADO_EN_ARCHIVO")));
        Assert.That(errores, Does.Contain((4, "PADRE_NO_EXISTE")));
        Assert.That(errores, Does.Contain((5, "CAMPO_REQUERIDO")));
        Assert.That(errores, Does.Contain((6, "FK_NO_EXISTE")));
        Assert.That(await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM ControlPresupuestario.CatalogoPartida WHERE Codigo IN ('9', '9.1', '8', '6')"), Is.Zero);
    }

    [Test]
    public async Task ImportarEstructura_PartidaExistenteConOtroNombreUOtroPadre_Devuelve422()
    {
        var (idPresupuesto, idVersion, _) = await CrearPresupuestoAsync();
        var ruta = $"{Base}/presupuestos/{idPresupuesto}/versiones/{idVersion}/partidas/importar-estructura";
        Assert.That((await SubirAsync(ruta, PresupuestoPorNiveles)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var otroNombre = await Errores(await SubirAsync(ruta, """
            Codigo;Nombre;Monto
            1;Gastos directos;
            1.1;OTRO NOMBRE;
            1.1.1;CONCRETO;10
            """));
        Assert.That(otroNombre, Does.Contain((2, "NOMBRE_DISTINTO")));
        Assert.That(otroNombre, Does.Not.Contain((1, "NOMBRE_DISTINTO")), "El nombre se compara sin mayúsculas ni tildes.");

        // 2.1 ya tiene monto en la versión: el SP del catálogo rechaza convertirla en categoría y el error vuelve por fila.
        var bajoHojaConMonto = await Errores(await SubirAsync(ruta, """
            Codigo;Nombre;Monto
            2;GASTOS INDIRECTOS;
            2.1;LICENCIA;
            2.1.1;LICENCIA HIJA;5
            """));
        Assert.That(bajoHojaConMonto, Is.EqualTo(new[] { (3, "PADRE_NO_DISPONIBLE") }));
        Assert.That(await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM ControlPresupuestario.CatalogoPartida WHERE Codigo = '2.1.1'"), Is.Zero);
    }

    private static async Task<List<(int, string?)>> Errores(HttpResponseMessage respuesta)
    {
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.UnprocessableEntity), await respuesta.Content.ReadAsStringAsync());
        return (await Json(respuesta)).GetProperty("errores").EnumerateArray()
            .Select(e => (e.GetProperty("fila").GetInt32(), e.GetProperty("codigoError").GetString())).ToList();
    }

    [Test]
    public async Task ArbolVigente_SumaLaEjecucionDeLasHojasEnSusCategorias()
    {
        var (idPresupuesto, idVersion, cc) = await CrearPresupuestoAsync();
        var ruta = $"{Base}/presupuestos/{idPresupuesto}/versiones/{idVersion}";
        Assert.That((await SubirAsync($"{ruta}/partidas/importar-estructura", PresupuestoPorNiveles)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await _client.PostAsync($"{ruta}/aprobar", null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var concreto = (await _client.GetFromJsonAsync<JsonElement>($"{ruta}/partidas")).EnumerateArray()
            .Single(p => p.GetProperty("codigo").GetString() == "1.1.1").GetProperty("idPresupuestoDetalle").GetInt32();
        var ajuste = await _client.PostAsJsonAsync($"{ruta}/partidas/{concreto}/ajustes",
            new { afectacion = "EJECUCION", direccion = "INCREMENTO", monto = 400m, observacion = "Factura de concreto" });
        Assert.That(ajuste.StatusCode, Is.EqualTo(HttpStatusCode.Created), await ajuste.Content.ReadAsStringAsync());

        var respuesta = await _client.GetAsync($"{Base}/consultas/arbol?idCentroCosto={cc}");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK), await respuesta.Content.ReadAsStringAsync());
        var arbol = await Json(respuesta);
        var nodos = arbol.GetProperty("nodos").EnumerateArray().ToDictionary(n => n.GetProperty("codigo").GetString()!);
        Assert.That(nodos["1.1"].GetProperty("montoEjecutado").GetDecimal(), Is.EqualTo(400m));
        Assert.That(nodos["1"].GetProperty("montoEjecutado").GetDecimal(), Is.EqualTo(400m));
        Assert.That(nodos["1"].GetProperty("saldoDisponible").GetDecimal(), Is.EqualTo(1350m));
        Assert.That(nodos["2"].GetProperty("montoEjecutado").GetDecimal(), Is.Zero);
        Assert.That(arbol.GetProperty("totales").GetProperty("montoEjecutado").GetDecimal(), Is.EqualTo(400m));
        Assert.That(arbol.GetProperty("encabezado").GetProperty("codigoMoneda").GetString(), Is.EqualTo("PEN"));

        Assert.That((await _client.GetAsync($"{Base}/consultas/arbol?idCentroCosto=0")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await _client.GetAsync($"{Base}/consultas/arbol?idCentroCosto=999999")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Dashboard_PorNivel_AgrupaLosRubrosEnSuCategoriaDeEseNivel()
    {
        var (idPresupuesto, idVersion, cc) = await CrearPresupuestoAsync();
        var ruta = $"{Base}/presupuestos/{idPresupuesto}/versiones/{idVersion}";
        Assert.That((await SubirAsync($"{ruta}/partidas/importar-estructura", PresupuestoPorNiveles)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await _client.PostAsync($"{ruta}/aprobar", null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var concreto = (await _client.GetFromJsonAsync<JsonElement>($"{ruta}/partidas")).EnumerateArray()
            .Single(p => p.GetProperty("codigo").GetString() == "1.1.1").GetProperty("idPresupuestoDetalle").GetInt32();
        await _client.PostAsJsonAsync($"{ruta}/partidas/{concreto}/ajustes",
            new { afectacion = "EJECUCION", direccion = "INCREMENTO", monto = 400m, observacion = "Factura de concreto" });

        async Task<Dictionary<string, (decimal Presupuestado, decimal Ejecutado)>> Rubros(string nivel)
        {
            var d = await _client.GetFromJsonAsync<JsonElement>($"{Base}/consultas/dashboard?idCentroCosto={cc}{nivel}");
            Assert.That(d.GetProperty("encabezado").GetProperty("nivelMaximo").GetInt32(), Is.EqualTo(3));
            return d.GetProperty("rubros").EnumerateArray().ToDictionary(r => r.GetProperty("codigo").GetString()!,
                r => (r.GetProperty("presupuestado").GetDecimal(), r.GetProperty("ejecutado").GetDecimal()));
        }

        var nivel1 = await Rubros("&nivel=1");
        Assert.That(nivel1.Keys, Is.EquivalentTo(new[] { "1", "2" }));
        Assert.That(nivel1["1"], Is.EqualTo((1750m, 400m)));
        Assert.That(nivel1["2"], Is.EqualTo((300m, 0m)));
        Assert.That((await Rubros("&nivel=2")).Keys, Is.EquivalentTo(new[] { "1.1", "1.2", "2.1" }));
        Assert.That((await Rubros("")).Keys, Is.EquivalentTo(new[] { "1.1.1", "1.1.2", "1.2.1", "2.1" }));
        Assert.That((await Rubros("&nivel=9")).Keys, Is.EquivalentTo(new[] { "1.1.1", "1.1.2", "1.2.1", "2.1" }),
            "Un nivel más profundo que el árbol deja cada rama en su hoja.");
        Assert.That((await _client.GetAsync($"{Base}/consultas/dashboard?idCentroCosto={cc}&nivel=0")).StatusCode,
            Is.EqualTo(HttpStatusCode.BadRequest));

        // Bajar a una rama: todo el tablero se limita a ella y se agrupa por sus hijas.
        var idDirectos = await DbHelpers.QueryScalarAsync<int>(
            "SELECT IdCatalogoPartida FROM ControlPresupuestario.CatalogoPartida WHERE Codigo = '1'");
        var rama = await _client.GetFromJsonAsync<JsonElement>($"{Base}/consultas/dashboard?idCentroCosto={cc}&idPartidaPadre={idDirectos}");
        Assert.That(rama.GetProperty("rubros").EnumerateArray().Select(r => r.GetProperty("codigo").GetString()),
            Is.EquivalentTo(new[] { "1.1", "1.2" }));
        Assert.That(rama.GetProperty("resumen").GetProperty("presupuestado").GetDecimal(), Is.EqualTo(1750m));
        Assert.That(rama.GetProperty("alCorte").GetProperty("realAcumulado").GetDecimal(), Is.EqualTo(400m));
        Assert.That(rama.GetProperty("encabezado").GetProperty("nivel").GetInt32(), Is.EqualTo(2));
        Assert.That(rama.GetProperty("encabezado").GetProperty("rama").EnumerateArray()
            .Select(r => r.GetProperty("codigo").GetString()), Is.EqualTo(new[] { "1" }));
        Assert.That((await _client.GetAsync($"{Base}/consultas/dashboard?idCentroCosto={cc}&idPartidaPadre=999999")).StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task PlantillaEstructura_TraeCategoriasConSubtotalYHojasConMonto()
    {
        var (idPresupuesto, idVersion, _) = await CrearPresupuestoAsync();
        var ruta = $"{Base}/presupuestos/{idPresupuesto}/versiones/{idVersion}/partidas";
        Assert.That((await SubirAsync($"{ruta}/importar-estructura", PresupuestoPorNiveles)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var csv = await _client.GetAsync($"{ruta}/plantilla-estructura?formato=csv");
        Assert.That(csv.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var lineas = (await csv.Content.ReadAsStringAsync()).TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r')).ToList();
        Assert.That(lineas[0], Is.EqualTo("Codigo;Nombre;Tipo;Seccion;Monto;Subtotal;Observacion"));
        Assert.That(lineas, Does.Contain("1.1;ESTRUCTURAS;MATERIALES;;;1500.00;"));
        Assert.That(lineas, Does.Contain("1.1.1;CONCRETO;MATERIALES;;1000.00;;"));

        var xlsx = await _client.GetAsync($"{ruta}/plantilla-estructura?formato=xlsx");
        Assert.That(xlsx.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(xlsx.Content.Headers.ContentType?.MediaType, Does.Contain("spreadsheetml"));
    }

    [Test]
    public async Task ImportarEstructura_SinPermisoDeCrearPartidas_Devuelve403()
    {
        var (idPresupuesto, idVersion, _) = await CrearPresupuestoAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            JwtTestTokenFactory.CrearToken(permisos: new[] { CP.Presupuesto.EditarDetalle }));

        var respuesta = await SubirAsync($"{Base}/presupuestos/{idPresupuesto}/versiones/{idVersion}/partidas/importar-estructura",
            PresupuestoPorNiveles);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    // ---------------------------------------------------------------- apoyo

    private Task<HttpResponseMessage> SubirAsync(string ruta, string csv)
    {
        var contenido = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(Encoding.UTF8.GetBytes(csv.Replace("\r\n", "\n")));
        archivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        contenido.Add(archivo, "archivo", "presupuesto.csv");
        contenido.Add(new StringContent("false"), "quitarAusentes");
        return _client.PostAsync(ruta, contenido);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<(int IdPresupuesto, int IdVersion, int IdCentroCosto)> CrearPresupuestoAsync()
    {
        var tipo = await DbHelpers.QueryScalarAsync<int>(
            "SELECT IdTipoCentroCosto FROM ControlPresupuestario.TipoCentroCosto WHERE Codigo = 'ADMINISTRACION'");
        var cc = await _client.PostAsJsonAsync($"{Base}/centros-costo", new { codigo = "CC-ARBOL", nombre = "Obra árbol", idTipoCentroCosto = tipo });
        Assert.That(cc.StatusCode, Is.EqualTo(HttpStatusCode.Created), await cc.Content.ReadAsStringAsync());
        var idCentro = (await Json(cc)).GetProperty("idCentroCosto").GetInt32();
        var p = await _client.PostAsJsonAsync($"{Base}/presupuestos", new
        {
            idCentroCosto = idCentro, idMoneda = _idPen, codigo = "PRES-ARBOL", nombre = "Presupuesto árbol",
            fechaInicio = "2026-01-01", fechaFin = "2026-12-31"
        });
        Assert.That(p.StatusCode, Is.EqualTo(HttpStatusCode.Created), await p.Content.ReadAsStringAsync());
        var creado = await Json(p);
        return (creado.GetProperty("idPresupuesto").GetInt32(), creado.GetProperty("idPresupuestoVersion").GetInt32(), idCentro);
    }
}
