using System.Text;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Tests.Unit.Importacion.Common;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion;

/// <summary>
/// Pruebas unitarias de <see cref="CsvFileParser"/>.
/// Cubre parseo correcto, errores de estructura (delimitador, encoding)
/// y la magia de deteccion por magic numbers.
/// </summary>
public class CsvFileParserTests
{
    private readonly CsvFileParser _sut = new();

    [Test]
    public void Parse_CsvConComaYPuntoYComa_EsRechazadoPorDelimitador()
    {
        // El archivo usa ';' como delimitador (caso tipico de Excel en espanol).
        var contenido = "Codigo;Nombre\nUM001;Kilogramo";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var ex = Assert.Throws<EstructuraInvalidaException>(() => _sut.Parse(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("FORMATO_INVALIDO"));
        Assert.That(ex.Message, Does.Contain("delimitador"));
    }

    [Test]
    public void Parse_CsvConTab_EsRechazadoPorDelimitador()
    {
        var contenido = "Codigo\tNombre\nUM001\tKilogramo";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var ex = Assert.Throws<EstructuraInvalidaException>(() => _sut.Parse(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("FORMATO_INVALIDO"));
    }

    [Test]
    public void Parse_CsvConPipe_EsRechazadoPorDelimitador()
    {
        var contenido = "Codigo|Nombre\nUM001|Kilogramo";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var ex = Assert.Throws<EstructuraInvalidaException>(() => _sut.Parse(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("FORMATO_INVALIDO"));
    }

    [Test]
    public void Parse_CsvLatin1_LanzaCodificacionInvalida()
    {
        // "ñ" en Latin-1 no es valido en UTF-8 estricto.
        var contenido = "Codigo,Nombre\nUM001,Año";
        var bytes = Encoding.Latin1.GetBytes(contenido);
        var archivo = TestFormFiles.FromBytes(bytes, "datos.csv", "text/csv");

        var ex = Assert.Throws<EstructuraInvalidaException>(() => _sut.Parse(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("CODIFICACION_INVALIDO"));
    }

    [Test]
    public void Parse_CsvUtf8ConBOM_ProcesaCorrectamente()
    {
        var contenido = "Codigo,Nombre,Activo\nUM001,Kilogramo,true\nUM002,Metro,false";
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes(contenido))
            .ToArray();
        var archivo = TestFormFiles.FromBytes(bytes, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
        Assert.That(filas[0].GetString("Codigo"), Is.EqualTo("UM001"));
        Assert.That(filas[0].GetString("Nombre"), Is.EqualTo("Kilogramo"));
        Assert.That(filas[0].GetBool("Activo"), Is.True);
        Assert.That(filas[1].GetString("Codigo"), Is.EqualTo("UM002"));
        Assert.That(filas[1].GetBool("Activo"), Is.False);
    }

    [Test]
    public void Parse_CsvUtf8SinBOM_ProcesaCorrectamente()
    {
        var contenido = "Codigo,Nombre\nUM001,Kilogramo\nUM002,Metro";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
        Assert.That(filas[0].NumeroFila, Is.EqualTo(1));
        Assert.That(filas[1].NumeroFila, Is.EqualTo(2));
    }

    [Test]
    public void Parse_CsvConCaracteresEspanolesEnUtf8_ProcesaCorrectamente()
    {
        var contenido = "Codigo,Nombre\nUM001,Año\nUM002,Niño";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
        Assert.That(filas[0].GetString("Nombre"), Is.EqualTo("Año"));
        Assert.That(filas[1].GetString("Nombre"), Is.EqualTo("Niño"));
    }

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
        var contenido = "Codigo,Nombre\nUM001,Kilogramo\n\n\nUM002,Metro\n";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
    }

    [Test]
    public void Parse_CsvConValoresFaltantes_LosCeldaComoVacia()
    {
        var contenido = "Codigo,Nombre,Descripcion\nUM001,Kilogramo,";
        var archivo = TestFormFiles.FromText(contenido, "datos.csv", "text/csv");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(1));
        Assert.That(filas[0].GetString("Descripcion"), Is.Null); // vacio -> null
    }

    [Test]
    public void PuedeParsear_CsvConBOM_DevuelveTrue()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a' };
        Assert.That(_sut.PuedeParsear(".csv", bytes), Is.True);
    }

    [Test]
    public void PuedeParsear_CsvAscii_DevuelveTrue()
    {
        var bytes = Encoding.ASCII.GetBytes("Codigo,Nombre\nUM001,Kilogramo");
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
        var bytes = Encoding.ASCII.GetBytes("a,b,c");
        Assert.That(_sut.PuedeParsear(".xlsx", bytes), Is.False);
    }
}
