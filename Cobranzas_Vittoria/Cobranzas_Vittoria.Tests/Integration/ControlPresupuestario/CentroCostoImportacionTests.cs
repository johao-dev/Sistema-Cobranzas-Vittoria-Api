using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cobranzas_Vittoria.Seguridad.Authorization;
using Cobranzas_Vittoria.Tests.Integration.Common;

namespace Cobranzas_Vittoria.Tests.Integration.ControlPresupuestario;

/// <summary>
/// Importación de centros de costo por CSV (/api/control-presupuestario/centros-costo/importar).
/// Respawn vacía los catálogos del módulo entre tests, así que cada test recrea los tipos;
/// los proyectos (maestra) no se limpian y se crean con nombre único.
/// </summary>
[NonParallelizable]
public sealed class CentroCostoImportacionTests : IntegrationTestBase
{
    private const string Url = "/api/control-presupuestario/centros-costo/importar";
    private const string Encabezado = "Codigo;Nombre;Tipo;Proyecto;Descripcion";

    [SetUp]
    public async Task Preparar()
    {
        UsarToken(new[] { Permisos.ControlPresupuestario.CentroCosto.Crear });
        await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO ControlPresupuestario.TipoCentroCosto (Codigo, Nombre)
            SELECT Codigo, Nombre FROM (VALUES ('PROYECTO', N'Proyecto'), ('ADMINISTRACION', N'Administración')) v(Codigo, Nombre)
            WHERE NOT EXISTS (SELECT 1 FROM ControlPresupuestario.TipoCentroCosto t WHERE t.Codigo = v.Codigo);
            SELECT 1;
            """);
    }

    private void UsarToken(IEnumerable<string>? permisos) =>
        _client.DefaultRequestHeaders.Authorization = permisos is null
            ? null
            : new AuthenticationHeaderValue("Bearer", JwtTestTokenFactory.CrearToken(permisos: permisos));

    [Test]
    public async Task ArchivoValido_CreaLosCentrosConTipoPorNombreYProyectoPorNombre()
    {
        var proyecto = await CrearProyectoAsync();

        var response = await SubirAsync(
            $"CC-P;Obra importada;PROYECTO;{proyecto.Nombre.ToUpperInvariant()};Casco\n" +
            "CC-A;Oficina importada;Administración;;");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        var cuerpo = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(cuerpo.GetProperty("filasInsertadas").GetInt32(), Is.EqualTo(2));
        var filas = (await DbHelpers.QueryAsync<(string Codigo, int? IdProyecto, string Tipo)>("""
            SELECT c.Codigo, c.IdProyecto, t.Codigo FROM ControlPresupuestario.CentroCosto c
            JOIN ControlPresupuestario.TipoCentroCosto t ON t.IdTipoCentroCosto = c.IdTipoCentroCosto ORDER BY c.Codigo
            """)).ToList();
        Assert.That(filas, Is.EqualTo(new[] { ("CC-A", (int?)null, "ADMINISTRACION"), ("CC-P", (int?)proyecto.Id, "PROYECTO") }));
    }

    [Test]
    public async Task ArchivoConErrores_LosInformaTodosPorFilaYNoInsertaNada()
    {
        var proyecto = await CrearProyectoAsync();
        var conCentro = await CrearProyectoAsync();
        await SubirAsync($"CC-EXISTE;Ya existe;PROYECTO;{conCentro.Nombre};");

        var response = await SubirAsync(
            $"CC-OK;Válida;PROYECTO;{proyecto.Nombre};\n" +     // 1: válida, pero no debe insertarse
            "CC-EXISTE;Repetida en BD;ADMINISTRACION;;\n" +       // 2
            "CC-X;Tipo raro;OPERACIONES;;\n" +                     // 3
            "CC-Y;Sin proyecto;PROYECTO;;\n" +                      // 4
            $"CC-Z;Admin con proyecto;ADMINISTRACION;{Guid.NewGuid():N};\n" + // 5 (proyecto inexistente)
            $"CC-W;Proyecto ocupado;PROYECTO;{conCentro.Nombre};\n" + // 6
            "CC-OK;Duplicada;ADMINISTRACION;;");                   // 7

        Assert.That(response.StatusCode, Is.EqualTo((HttpStatusCode)422));
        var errores = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errores")
            .EnumerateArray().Select(e => (e.GetProperty("fila").GetInt32(), e.GetProperty("campo").GetString())).ToList();
        Assert.That(errores, Is.SupersetOf(new[]
        {
            (2, "Codigo"), (3, "Tipo"), (4, "Proyecto"), (5, "Proyecto"), (6, "Proyecto"), (7, "Codigo")
        }));
        Assert.That(errores.Any(e => e.Item1 == 1), Is.False, "La fila válida no reporta errores.");
        Assert.That(await DbHelpers.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM ControlPresupuestario.CentroCosto WHERE Codigo = 'CC-OK'"), Is.Zero);
    }

    [Test]
    public async Task TipoNoProyectoConProyecto_Rechazado()
    {
        var proyecto = await CrearProyectoAsync();

        var response = await SubirAsync($"CC-A;Oficina;ADMINISTRACION;{proyecto.Nombre};");

        Assert.That(response.StatusCode, Is.EqualTo((HttpStatusCode)422));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Solo el tipo PROYECTO admite proyecto"));
    }

    [Test]
    public async Task SinSesionOSinPermiso_Rechazado()
    {
        UsarToken(null);
        Assert.That((await SubirAsync("CC-A;Oficina;ADMINISTRACION;;")).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        UsarToken(new[] { Permisos.ControlPresupuestario.CentroCosto.Ver });
        Assert.That((await SubirAsync("CC-A;Oficina;ADMINISTRACION;;")).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Plantilla_TraeLosEncabezados()
    {
        var texto = (await _client.GetStringAsync("/api/control-presupuestario/centros-costo/plantilla")).TrimStart('﻿');
        Assert.That(texto, Does.StartWith(Encabezado));
    }

    private async Task<HttpResponseMessage> SubirAsync(string filas)
    {
        using var form = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(Encoding.UTF8.GetBytes(Encabezado + "\n" + filas + "\n"));
        archivo.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(archivo, "archivo", "centros.csv");
        return await _client.PostAsync(Url, form);
    }

    private static async Task<(int Id, string Nombre)> CrearProyectoAsync()
    {
        var nombre = "Proyecto import " + Guid.NewGuid().ToString("N")[..8];
        var id = await DbHelpers.QueryScalarAsync<int>("""
            INSERT INTO maestra.Proyecto (NombreProyecto, Activo, FechaCreacion, CotizacionGeneral)
            VALUES (@nombre, 1, SYSDATETIME(), 0);
            SELECT CONVERT(INT, SCOPE_IDENTITY());
            """, new { nombre });
        return (id, nombre);
    }
}
