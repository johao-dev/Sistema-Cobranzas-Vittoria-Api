using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Tests.Unit.Middleware;

/// <summary>
/// Pruebas unitarias de <see cref="ApiExceptionMiddleware"/>.
///
/// A diferencia de <c>ApiExceptionMiddlewareTests</c> (integration), estos tests
/// invocan el middleware directamente con un <see cref="DefaultHttpContext"/>
/// y un <see cref="RequestDelegate"/> stub que lanza la excepcion bajo prueba.
/// No requieren SQL Server ni pipeline HTTP completo, por lo que son rapidos
/// y aíslan la logica de mapeo de excepciones -> respuesta JSON.
///
/// Cobertura:
///   - ArchivoInvalidoException (TAMANIO_EXCEDIDO -> 413, otros -> 400)
///   - EstructuraInvalidaException (400)
///   - DatosInvalidosException (422 con array "errores")
///   - ModuloNoSoportadoException (400 con "MODULO_NO_SOPORTADO")
///   - Regresion: SqlException -> 500 SQL_ERROR, Exception generica -> 500 UNHANDLED_ERROR
///   - Sin excepcion: el middleware NO escribe respuesta (delega en el pipeline)
/// </summary>
public class ApiExceptionMiddlewareUnitTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Test]
    public async Task ArchivoInvalido_TamanioExcedido_Devuelve413ConCodigoTamanioExcedido()
    {
        var ctx = await InvocarConExcepcion(new ArchivoInvalidoException("TAMANIO_EXCEDIDO", "Archivo supera 10 MB."));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status413PayloadTooLarge));
        var body = await LeerBodyAsync(ctx);
        Assert.That(ObtenerString(body, "error"), Is.EqualTo("TAMANIO_EXCEDIDO"));
        Assert.That(ObtenerString(body, "message"), Is.EqualTo("Archivo supera 10 MB."));
    }

    [Test]
    public async Task ArchivoInvalido_ExtensionInvalida_Devuelve400ConCodigoExtensionInvalida()
    {
        var ctx = await InvocarConExcepcion(new ArchivoInvalidoException("EXTENSION_INVALIDA", "Solo se permiten .csv, .xlsx, .xls."));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var body = await LeerBodyAsync(ctx);
        Assert.That(ObtenerString(body, "error"), Is.EqualTo("EXTENSION_INVALIDA"));
    }

    [Test]
    public async Task ArchivoInvalido_MimeInvalido_Devuelve400ConCodigoMimeInvalido()
    {
        var ctx = await InvocarConExcepcion(new ArchivoInvalidoException("MIME_INVALIDO", "El MIME del archivo no coincide con su extension."));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var body = await LeerBodyAsync(ctx);
        Assert.That(ObtenerString(body, "error"), Is.EqualTo("MIME_INVALIDO"));
    }

    [Test]
    public async Task ArchivoInvalido_ArchivoVacio_Devuelve400ConCodigoArchivoVacio()
    {
        var ctx = await InvocarConExcepcion(new ArchivoInvalidoException("ARCHIVO_VACIO", "El archivo no contiene datos."));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var body = await LeerBodyAsync(ctx);
        Assert.That(ObtenerString(body, "error"), Is.EqualTo("ARCHIVO_VACIO"));
    }

    [Test]
    public async Task EstructuraInvalida_Devuelve400ConCodigoPropagado()
    {
        var ctx = await InvocarConExcepcion(new EstructuraInvalidaException("ENCABEZADOS_INCORRECTOS", "Falta la columna Codigo."));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var body = await LeerBodyAsync(ctx);
        Assert.That(ObtenerString(body, "error"), Is.EqualTo("ENCABEZADOS_INCORRECTOS"));
        Assert.That(ObtenerString(body, "message"), Is.EqualTo("Falta la columna Codigo."));
    }

    [Test]
    public async Task DatosInvalidos_Devuelve422ConArrayDeErrores()
    {
        var errores = new List<DetalleErrorFila>
        {
            new(5, "Nombre", "CAMPO_OBLIGATORIO", "El campo Nombre es obligatorio."),
            new(12, "Monto", "VALOR_FUERA_DE_RANGO", "Monto debe ser mayor a 0.")
        };
        var ctx = await InvocarConExcepcion(new DatosInvalidosException("Se encontraron 2 errores en el archivo.", errores));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status422UnprocessableEntity));
        var body = await LeerBodyAsync(ctx);
        Assert.That(ObtenerString(body, "error"), Is.EqualTo("DATOS_INVALIDOS"));
        Assert.That(ObtenerString(body, "message"), Is.EqualTo("Se encontraron 2 errores en el archivo."));

        Assert.That(body.TryGetProperty("errores", out var erroresJson), Is.True);
        Assert.That(erroresJson.ValueKind, Is.EqualTo(JsonValueKind.Array));
        Assert.That(erroresJson.GetArrayLength(), Is.EqualTo(2));

        var primero = erroresJson[0];
        Assert.That(primero.GetProperty("fila").GetInt32(), Is.EqualTo(5));
        Assert.That(primero.GetProperty("campo").GetString(), Is.EqualTo("Nombre"));
        Assert.That(primero.GetProperty("codigoError").GetString(), Is.EqualTo("CAMPO_OBLIGATORIO"));
        Assert.That(primero.GetProperty("mensaje").GetString(), Is.EqualTo("El campo Nombre es obligatorio."));

        var segundo = erroresJson[1];
        Assert.That(segundo.GetProperty("fila").GetInt32(), Is.EqualTo(12));
        Assert.That(segundo.GetProperty("campo").GetString(), Is.EqualTo("Monto"));
        Assert.That(segundo.GetProperty("codigoError").GetString(), Is.EqualTo("VALOR_FUERA_DE_RANGO"));
    }

    [Test]
    public async Task DatosInvalidos_ConListaVacia_Devuelve422YArrayVacio()
    {
        var ctx = await InvocarConExcepcion(new DatosInvalidosException("Sin detalle.", Array.Empty<DetalleErrorFila>()));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status422UnprocessableEntity));
        var body = await LeerBodyAsync(ctx);
        Assert.That(body.GetProperty("errores").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task ModuloNoSoportado_Devuelve400ConCodigoModuloNoSoportado()
    {
        var ctx = await InvocarConExcepcion(new ModuloNoSoportadoException("El modulo 'Foo' no esta habilitado para carga masiva."));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        var body = await LeerBodyAsync(ctx);
        Assert.That(ObtenerString(body, "error"), Is.EqualTo("MODULO_NO_SOPORTADO"));
    }

    [Test]
    public async Task SqlException_Devuelve500ConCodigoSqlError_Regresion()
    {
        // SqlException es sealed y en Microsoft.Data.SqlClient 6.1.4 no expone constructores publicos
        // utiles para tests unitarios. Usamos RuntimeHelpers.GetUninitializedObject para crear una
        // instancia sin invocar constructor y luego seteamos el campo privado _message de Exception
        // para que el middleware lo incluya en la respuesta JSON.
        // Esto valida que el catch (SqlException) del middleware se ejecuta ANTES del catch (Exception)
        // y produce "SQL_ERROR" en vez de "UNHANDLED_ERROR" (regresion).
        var sqlEx = CrearSqlException("Error de SQL simulado.");

        var ctx = await InvocarConExcepcion(sqlEx);

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        var body = await LeerBodyAsync(ctx);
        Assert.That(ObtenerString(body, "error"), Is.EqualTo("SQL_ERROR"));
        Assert.That(ObtenerString(body, "message"), Does.Contain("Error de SQL simulado"));
    }

    private static SqlException CrearSqlException(string message)
    {
        // Crea una instancia de SqlException sin invocar ningun constructor.
        // SqlException es sealed y sus constructores internos cambian entre versiones de
        // Microsoft.Data.SqlClient; este enfoque es independiente de la firma del constructor.
        var instance = (SqlException)RuntimeHelpers.GetUninitializedObject(typeof(SqlException));

        // Set _message de Exception para que Message getter devuelva el texto correcto.
        var messageField = typeof(Exception).GetField("_message", BindingFlags.Instance | BindingFlags.NonPublic);
        messageField?.SetValue(instance, message);

        return instance;
    }

    [Test]
    public async Task ExceptionGenerica_Devuelve500ConCodigoUnhandledError_Regresion()
    {
        var ctx = await InvocarConExcepcion(new InvalidOperationException("Debes ingresar el nombre."));

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
        var body = await LeerBodyAsync(ctx);
        Assert.That(ObtenerString(body, "error"), Is.EqualTo("UNHANDLED_ERROR"));
        Assert.That(ObtenerString(body, "message"), Is.EqualTo("Debes ingresar el nombre."));
    }

    [Test]
    public async Task SinExcepcion_NoModificaLaRespuesta()
    {
        var middleware = new ApiExceptionMiddleware();
        var ctx = new DefaultHttpContext();
        ctx.Response.Body = new MemoryStream();
        var next = new RequestDelegate(_ => { ctx.Response.StatusCode = 200; return Task.CompletedTask; });

        await middleware.InvokeAsync(ctx, next);

        Assert.That(ctx.Response.StatusCode, Is.EqualTo(200));
        // El body no fue tocado por el middleware: el delegado de next puso el status pero no escribio body.
        ctx.Response.Body.Position = 0;
        Assert.That(ctx.Response.Body.Length, Is.EqualTo(0));
    }

    // --- Helpers ---

    private static async Task<HttpContext> InvocarConExcepcion(Exception ex)
    {
        var middleware = new ApiExceptionMiddleware();
        var ctx = new DefaultHttpContext
        {
            Response = { Body = new MemoryStream() }
        };
        RequestDelegate next = _ => throw ex;
        await middleware.InvokeAsync(ctx, next);
        return ctx;
    }

    private static async Task<JsonElement> LeerBodyAsync(HttpContext ctx)
    {
        ctx.Response.Body.Position = 0;
        using var reader = new StreamReader(ctx.Response.Body, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<JsonElement>(json, JsonOpts);
    }

    /// <summary>
    /// Obtiene una propiedad string del JSON con busqueda case-insensitive,
    /// reutilizable fuera de <c>Integration/Common</c>.
    /// </summary>
    private static string ObtenerString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var match))
            return match.GetString() ?? string.Empty;

        foreach (var prop in element.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                return prop.Value.GetString() ?? string.Empty;
        }

        throw new KeyNotFoundException($"No se encontro la propiedad '{propertyName}'.");
    }
}
