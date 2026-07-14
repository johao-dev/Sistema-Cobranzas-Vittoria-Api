using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Tests.Unit.Importacion.Common;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion;

/// <summary>
/// Pruebas unitarias de <see cref="FileParserResolver"/>.
/// Verifica que el resolver selecciona el parser correcto segun extension + magic numbers.
/// </summary>
public class FileParserResolverTests
{
    private readonly FileParserResolver _sut = new(new IFileParser[] { new CsvFileParser(), new ExcelFileParser() });

    [Test]
    public void ObtenerParser_ArchivoCsv_DevuelveCsvFileParser()
    {
        var archivo = TestFormFiles.FromText("Codigo,Nombre\nUM001,X", "datos.csv", "text/csv");
        var parser = _sut.ObtenerParser(archivo);
        Assert.That(parser, Is.InstanceOf<CsvFileParser>());
    }

    [Test]
    public void ObtenerParser_ArchivoXlsx_DevuelveExcelFileParser()
    {
        var bytes = TestFormFiles.BuildXlsx(headers: new[] { "Codigo" });
        var archivo = TestFormFiles.FromBytes(bytes, "datos.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var parser = _sut.ObtenerParser(archivo);
        Assert.That(parser, Is.InstanceOf<ExcelFileParser>());
    }

    [Test]
    public void ObtenerParser_ArchivoXls_DevuelveExcelFileParser()
    {
        var bytes = TestFormFiles.BuildXls(headers: new[] { "Codigo" });
        var archivo = TestFormFiles.FromBytes(bytes, "datos.xls", "application/vnd.ms-excel");
        var parser = _sut.ObtenerParser(archivo);
        Assert.That(parser, Is.InstanceOf<ExcelFileParser>());
    }

    [Test]
    public void ObtenerParser_ExtensionNoSoportada_LanzaFormatoInvalido()
    {
        var archivo = TestFormFiles.FromText("hola", "datos.txt", "text/plain");
        var ex = Assert.Throws<EstructuraInvalidaException>(() => _sut.ObtenerParser(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("FORMATO_INVALIDO"));
    }

    [Test]
    public void ObtenerParser_CsvConContenidoBinario_LanzaFormatoInvalido()
    {
        // .csv declarado pero el contenido es un PDF.
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 };
        var archivo = TestFormFiles.FromBytes(pdfBytes, "datos.csv", "text/csv");

        var ex = Assert.Throws<EstructuraInvalidaException>(() => _sut.ObtenerParser(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("FORMATO_INVALIDO"));
    }

    [Test]
    public void ObtenerParser_ExtensionMayuscula_DevuelveParserCorrecto()
    {
        // .CSV en mayusculas debe resolverse igual que .csv
        var archivo = TestFormFiles.FromText("Codigo,Nombre\nUM001,X", "datos.CSV", "text/csv");
        var parser = _sut.ObtenerParser(archivo);
        Assert.That(parser, Is.InstanceOf<CsvFileParser>());
    }
}
