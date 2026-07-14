using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Tests.Unit.Importacion.Common;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion;

/// <summary>
/// Pruebas unitarias de <see cref="ExcelFileParser"/>.
/// Cubre parseo de .xlsx y .xls, conversion de tipos y errores de formato.
/// </summary>
public class ExcelFileParserTests
{
    private readonly ExcelFileParser _sut = new();

    [Test]
    public void Parse_XlsxValido_DevuelveFilasConEncabezados()
    {
        var bytes = TestFormFiles.BuildXlsx(
            headers: new[] { "Codigo", "Nombre", "Activo" },
            rows: new[]
            {
                new[] { "UM001", "Kilogramo", "true" },
                new[] { "UM002", "Metro", "false" }
            });
        var archivo = TestFormFiles.FromBytes(bytes, "datos.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
        Assert.That(filas[0].GetString("Codigo"), Is.EqualTo("UM001"));
        Assert.That(filas[0].GetString("Nombre"), Is.EqualTo("Kilogramo"));
        Assert.That(filas[0].GetBool("Activo"), Is.True);
        Assert.That(filas[1].GetBool("Activo"), Is.False);
    }

    [Test]
    public void Parse_XlsValido_DevuelveFilasConEncabezados()
    {
        var bytes = TestFormFiles.BuildXls(
            headers: new[] { "Codigo", "Nombre" },
            rows: new[]
            {
                new[] { "UM001", "Kilogramo" },
                new[] { "UM002", "Metro" }
            });
        var archivo = TestFormFiles.FromBytes(bytes, "datos.xls", "application/vnd.ms-excel");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
        Assert.That(filas[0].GetString("Codigo"), Is.EqualTo("UM001"));
    }

    [Test]
    public void Parse_XlsxConValorNumericoDecimal_SeFormateaConInvariantCulture()
    {
        // 1234.56 (con punto, formato en-US)
        var bytes = TestFormFiles.BuildXlsx(
            headers: new[] { "Codigo", "Monto" },
            rows: new[]
            {
                new[] { "X", "1234.56" }
            });
        var archivo = TestFormFiles.FromBytes(bytes, "datos.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var filas = _sut.Parse(archivo);

        Assert.That(filas[0].GetDecimal("Monto"), Is.EqualTo(1234.56m));
    }

    [Test]
    public void Parse_XlsxVacio_DevuelveListaVacia()
    {
        var bytes = TestFormFiles.BuildXlsx(headers: new[] { "Codigo" });
        var archivo = TestFormFiles.FromBytes(bytes, "vacio.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Is.Empty);
    }

    [Test]
    public void Parse_XlsxConFilasEnBlanco_LasOmite()
    {
        // Construimos un xlsx con una fila en blanco entre dos con datos.
        var bytes = TestFormFiles.BuildXlsx(
            headers: new[] { "Codigo" },
            rows: new[]
            {
                new[] { "UM001" },
                new[] { "" },
                new[] { "UM002" }
            });
        var archivo = TestFormFiles.FromBytes(bytes, "datos.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var filas = _sut.Parse(archivo);

        Assert.That(filas, Has.Count.EqualTo(2));
    }

    [Test]
    public void Parse_BytesNoSonExcel_LanzaFormatoInvalido()
    {
        // Bytes de texto plano -> no son ni xlsx ni xls.
        var archivo = TestFormFiles.FromText("esto no es excel", "datos.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var ex = Assert.Throws<EstructuraInvalidaException>(() => _sut.Parse(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("FORMATO_INVALIDO"));
    }

    [Test]
    public void Parse_XlsxSano_DevuelveNumeroDeFilaCorrecto()
    {
        var bytes = TestFormFiles.BuildXlsx(
            headers: new[] { "Codigo" },
            rows: new[]
            {
                new[] { "A" },
                new[] { "B" },
                new[] { "C" }
            });
        var archivo = TestFormFiles.FromBytes(bytes, "datos.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        var filas = _sut.Parse(archivo);

        Assert.That(filas[0].NumeroFila, Is.EqualTo(1));
        Assert.That(filas[1].NumeroFila, Is.EqualTo(2));
        Assert.That(filas[2].NumeroFila, Is.EqualTo(3));
    }

    [Test]
    public void PuedeParsear_XlsxConMagicNumberCorrecto_DevuelveTrue()
    {
        // PK\x03\x04
        var bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00 };
        Assert.That(_sut.PuedeParsear(".xlsx", bytes), Is.True);
    }

    [Test]
    public void PuedeParsear_XlsConMagicNumberCorrecto_DevuelveTrue()
    {
        // D0 CF 11 E0 A1 B1 1A E1
        var bytes = new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 };
        Assert.That(_sut.PuedeParsear(".xls", bytes), Is.True);
    }

    [Test]
    public void PuedeParsear_CsvConExtensionXlsx_DevuelveFalse()
    {
        // CSV (texto) renombrado a .xlsx
        var bytes = System.Text.Encoding.ASCII.GetBytes("Codigo,Nombre\nUM001,Kilogramo");
        Assert.That(_sut.PuedeParsear(".xlsx", bytes), Is.False);
    }
}
