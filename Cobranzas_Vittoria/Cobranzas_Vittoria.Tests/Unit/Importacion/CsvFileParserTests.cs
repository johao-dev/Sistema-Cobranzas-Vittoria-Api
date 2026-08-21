using System.Text;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Tests.Unit.Importacion.Common;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion;

/// <summary>
/// Pruebas unitarias de <see cref="CsvFileParser"/>.
///
/// Cobertura de la Fase 3 del refactor de importacion v2:
///   - Deteccion automatica de delimitador (';' default, ',' fallback).
///   - Encoding dual: UTF-8 (estricto, con o sin BOM) y Windows-1252 (fallback).
///   - Rechazo de tabulador y pipe como delimitadores.
///   - Deteccion por magic numbers (rechazo de binarios disfrazados de .csv).
/// </summary>
public class CsvFileParserTests
{
    private readonly CsvFileParser _sut = new();

    // ====================================================================
    // Delimitador: ';' (default, el caso tipico de Excel en espanol)
    // ====================================================================

    [Test]
    public void Parse_CsvConPuntoYComaUtf8_ProcesaCorrectamente()
    {
        var contenido = "Codigo;Nombre;Activo\r\nUM001;Kilogramo;true\r\nUM002;Metro;false";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
        Assert.That(filas[0].GetString("Codigo"), Is.EqualTo("UM001"));
        Assert.That(filas[0].GetString("Nombre"), Is.EqualTo("Kilogramo"));
        Assert.That(filas[0].GetBool("Activo"), Is.True);
        Assert.That(filas[1].GetBool("Activo"), Is.False);
    }

    [Test]
    public void Parse_CsvConPuntoYComaYWindows1252_ProcesaConTildes()
    {
        // "Año" y "Niño" codificados en Windows-1252 (sin BOM).
        // En Windows-1252, "ñ" es 0xF1 y "ó" es 0xF3.
        var contenido = "Codigo;Nombre\r\nUM001;Año\r\nUM002;Niño";
        var bytes = Encoding.GetEncoding("Windows-1252").GetBytes(contenido);
        var archivo = TestFormFiles.FromBytes(bytes, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
        Assert.That(filas[0].GetString("Nombre"), Is.EqualTo("Año"));
        Assert.That(filas[1].GetString("Nombre"), Is.EqualTo("Niño"));
    }

    [Test]
    public void Parse_CsvConTildesEnWindows1252_ProcesaCorrectamente()
    {
        // Solo tildes castellanas basicas: a, e, i, o, u, n con tilde.
        var contenido = "Especialidad;Nombre;UnidadMedida;Codigo\r\n" +
                        "Albañileria;Martillo;Kilogramo;MAT-001\r\n" +
                        "Carpinteria;Serrucho;Metro;MAT-002\r\n" +
                        "Estructura;Viga;Tonelada;MAT-003";
        var bytes = Encoding.GetEncoding("Windows-1252").GetBytes(contenido);
        var archivo = TestFormFiles.FromBytes(bytes, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(3));
        Assert.That(filas[0].GetString("Especialidad"), Is.EqualTo("Albañileria"));
        Assert.That(filas[1].GetString("Especialidad"), Is.EqualTo("Carpinteria"));
    }

    // ====================================================================
    // Delimitador: ',' (fallback, CSV en-US)
    // ====================================================================

    [Test]
    public void Parse_CsvConComa_ProcesaCorrectamente()
    {
        var contenido = "Codigo,Nombre\nUM001,Kilogramo\nUM002,Metro";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
        Assert.That(filas[0].NumeroFila, Is.EqualTo(1));
        Assert.That(filas[1].NumeroFila, Is.EqualTo(2));
    }

    [Test]
    public void Parse_CsvConPuntoYComaYComa_EligePuntoYComa()
    {
        // Header tiene ambos delimitadores, pero mas ';' que ','.
        // (Ej: "Codigo;Nombre,Apellido" -> 1 ';' y 1 ',' -> se elige ';')
        // En este caso hay mas ';' que ',', gana ';'.
        var contenido = "Codigo;Nombre;Apellido,Segundo\r\nUM001;Juan;Perez, Lopez";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        // Con ';' como delimitador, el header son 3 campos: Codigo, Nombre, Apellido,Segundo
        Assert.That(filas, Has.Count.EqualTo(1));
        Assert.That(filas[0].GetString("Codigo"), Is.EqualTo("UM001"));
        Assert.That(filas[0].GetString("Nombre"), Is.EqualTo("Juan"));
        Assert.That(filas[0].GetString("Apellido,Segundo"), Is.EqualTo("Perez, Lopez"));
    }

    [Test]
    public void Parse_CsvSinDelimitadoresEnPrimeraLinea_UsaPuntoYComaDefault()
    {
        // Si el archivo no tiene ';' ni ',' en el header, se usa ';' por default.
        // Una sola columna por fila.
        var contenido = "Codigo\r\nUM001\r\nUM002";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
        Assert.That(filas[0].GetString("Codigo"), Is.EqualTo("UM001"));
        Assert.That(filas[1].GetString("Codigo"), Is.EqualTo("UM002"));
    }

    // ====================================================================
    // Delimitador: rechazo de tabulador y pipe
    // ====================================================================

    [Test]
    public void Parse_CsvConTab_EsRechazadoPorDelimitadorNoSoportado()
    {
        var contenido = "Codigo\tNombre\nUM001\tKilogramo";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var ex = Assert.Throws<EstructuraInvalidaException>(() => _sut.Parse(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("FORMATO_INVALIDO"));
        Assert.That(ex.Message, Does.Contain("tabulador"));
    }

    [Test]
    public void Parse_CsvConPipe_EsRechazadoPorDelimitadorNoSoportado()
    {
        var contenido = "Codigo|Nombre\nUM001|Kilogramo";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var ex = Assert.Throws<EstructuraInvalidaException>(() => _sut.Parse(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("FORMATO_INVALIDO"));
        Assert.That(ex.Message, Does.Contain("pipe"));
    }

    // ====================================================================
    // Encoding dual: UTF-8 con BOM, sin BOM y Windows-1252
    // ====================================================================

    [Test]
    public void Parse_CsvUtf8ConBOM_ProcesaCorrectamente()
    {
        var contenido = "Codigo;Nombre;Activo\r\nUM001;Kilogramo;true\r\nUM002;Metro;false";
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes(contenido))
            .ToArray();
        var archivo = TestFormFiles.FromBytes(bytes, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
        Assert.That(filas[0].GetString("Codigo"), Is.EqualTo("UM001"));
        Assert.That(filas[0].GetBool("Activo"), Is.True);
        Assert.That(filas[1].GetBool("Activo"), Is.False);
    }

    [Test]
    public void Parse_CsvUtf8SinBOM_ProcesaCorrectamente()
    {
        var contenido = "Codigo;Nombre\r\nUM001;Kilogramo\r\nUM002;Metro";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
        Assert.That(filas[0].NumeroFila, Is.EqualTo(1));
        Assert.That(filas[1].NumeroFila, Is.EqualTo(2));
    }

    [Test]
    public void Parse_CsvConCaracteresEspanolesEnUtf8_ProcesaCorrectamente()
    {
        var contenido = "Codigo;Nombre\r\nUM001;Año\r\nUM002;Niño";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
        Assert.That(filas[0].GetString("Nombre"), Is.EqualTo("Año"));
        Assert.That(filas[1].GetString("Nombre"), Is.EqualTo("Niño"));
    }

    [Test]
    public void Parse_CsvConWindows1252ConBOM_EsAceptadoComoUtf8()
    {
        // Si el archivo trae BOM UTF-8 pero el cuerpo esta en Windows-1252, se
        // decodifica como UTF-8. Esto producira caracteres de reemplazo
        // (�) porque los bytes Windows-1252 no son UTF-8 validos, pero el
        // parseo NO debe lanzar excepcion (la regla de Fase 3 es: BOM gana).
        var contenido = "Codigo;Nombre\r\nUM001;Año";
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.GetEncoding("Windows-1252").GetBytes(contenido))
            .ToArray();
        var archivo = TestFormFiles.FromBytes(bytes, "datos.csv", "text/csv");

        // No debe lanzar: la presencia de BOM UTF-8 fuerza ese encoding.
        Assert.DoesNotThrow(() => _sut.Parse(archivo));
    }

    // ====================================================================
    // Casos limite y errores varios
    // ====================================================================

    [Test]
    public void Parse_CsvVacio_DevuelveListaVacia()
    {
        var archivo = TestFormFiles.FromText("", "vacio.csv", "text/csv");
        var filas = _sut.Parse(archivo);
        Assert.That(filas, Is.Empty);
    }

    [Test]
    public void Parse_CsvConFilasEnBlanco_LasOmite()
    {
        var contenido = "Codigo;Nombre\r\nUM001;Kilogramo\r\n\r\n\r\nUM002;Metro\r\n";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
    }

    [Test]
    public void Parse_CsvConValoresFaltantes_LosCeldaComoVacia()
    {
        var contenido = "Codigo;Nombre;Descripcion\r\nUM001;Kilogramo;";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(1));
        Assert.That(filas[0].GetString("Descripcion"), Is.Null); // vacio -> null
    }

    // ====================================================================
    // PuedeParsear: deteccion por magic numbers
    // ====================================================================

    [Test]
    public void PuedeParsear_CsvConBOM_DevuelveTrue()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a' };
        Assert.That(_sut.PuedeParsear(".csv", bytes), Is.True);
    }

    [Test]
    public void PuedeParsear_CsvAscii_DevuelveTrue()
    {
        var bytes = Encoding.ASCII.GetBytes("Codigo;Nombre\nUM001;Kilogramo");
        Assert.That(_sut.PuedeParsear(".csv", bytes), Is.True);
    }

    [Test]
    public void PuedeParsear_BinarioDisfraciadoDeCsv_DevuelveFalse()
    {
        // PDF magic number: %PDF
        var bytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        Assert.That(_sut.PuedeParsear(".csv", bytes), Is.False);
    }

    [Test]
    public void PuedeParsear_ExtensionNoCsv_DevuelveFalse()
    {
        var bytes = Encoding.ASCII.GetBytes("a;b;c");
        Assert.That(_sut.PuedeParsear(".xlsx", bytes), Is.False);
    }
}
