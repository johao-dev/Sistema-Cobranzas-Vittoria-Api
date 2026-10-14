using System.Globalization;
using System.IO.Compression;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Contable.GastosDirectos.Application.Importar;
using Cobranzas_Vittoria.Domain.Importacion;
using Microsoft.AspNetCore.Http;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion;

public sealed class GastosDirectosExcelTests
{
    [Test]
    public void PlantillaOriginalVacia_NoImportaAuxiliaresNiEjemplos()
    {
        using var libro = Libro();
        Assert.That(Leer(libro), Is.Empty);
    }

    [Test]
    public void BuscaHojaPorNombreAunqueNoSeaLaPrimera()
    {
        using var libro = Libro();
        libro.SetSheetOrder("Ejemplos", 0);
        Poner(libro, 8, "Concepto", "  Gasto  ");
        var filas = Leer(libro);
        Assert.That(filas.Count, Is.EqualTo(1));
        Assert.That(filas[0].NumeroFila, Is.EqualTo(8));
        Assert.That(filas[0].GetString("Concepto"), Is.EqualTo("Gasto"));
        Assert.That(filas[0].ContieneColumna("Registro"), Is.False);
        Assert.That(filas[0].ContieneColumna("Estado de fila"), Is.False);
    }

    [Test]
    public void FormulaConResultadoVacio_SeConsideraAusente()
    {
        using var libro = Libro();
        Poner(libro, 6, "Concepto", " ");
        var celda = Poner(libro, 7, "Monto", "");
        celda.SetCellFormula("IF(1=1,\"\",100)");
        libro.GetCreationHelper().CreateFormulaEvaluator().EvaluateFormulaCell(celda);
        Assert.That(Leer(libro), Is.Empty);
    }

    [Test]
    public void FormulaConError_NoSeInterpretaComoCeldaVacia()
    {
        using var libro = Libro();
        var celda = Poner(libro, 6, "Monto", "");
        celda.SetCellFormula("1/0");
        libro.GetCreationHelper().CreateFormulaEvaluator().EvaluateFormulaCell(celda);
        Assert.That(Leer(libro)[0].GetString("Monto"), Is.EqualTo("#CELDA_INVALIDA"));
    }

    [TestCase("es-PE")]
    [TestCase("de-DE")]
    [TestCase("en-US")]
    public void NumerosNativosUsanDecimalSinDependerDeCultura(string cultura)
    {
        var antes = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultura);
            using var libro = Libro();
            var celda = (XSSFCell)Poner(libro, 6, "Tipo de cambio", "");
            celda.SetCellValue(3.76);
            var filas = Leer(libro, "3.7599999999999998");
            Assert.That(filas[0].GetString("Tipo de cambio"), Is.EqualTo("3.76"));
        }
        finally { CultureInfo.CurrentCulture = antes; }
    }

    [Test]
    public void ImporteTextoConservaPrecisionDecimalYNoSeRedondea()
    {
        using var libro = Libro();
        Poner(libro, 6, "Monto", "9999999999999999.99");
        Assert.That(Leer(libro)[0].GetString("Monto"), Is.EqualTo("9999999999999999.99"));
    }

    [Test]
    public void FechaNativaConHora_UsaFechaCalendarioDeExcel()
    {
        using var libro = Libro();
        var celda = Poner(libro, 6, "Fecha", "");
        celda.SetCellValue(new DateTime(2026, 10, 5, 12, 0, 0));
        var estilo = libro.CreateCellStyle();
        estilo.DataFormat = libro.CreateDataFormat().GetFormat("dd/mm/yyyy");
        celda.CellStyle = estilo;
        Assert.That(Leer(libro)[0].GetString("Fecha"), Is.EqualTo("05/10/2026"));
    }

    [Test]
    public void HojaAusente_Rechaza()
    {
        using var libro = Libro();
        libro.SetSheetName(0, "Otra hoja");
        Assert.Throws<EstructuraInvalidaException>(() => Leer(libro));
    }

    [Test]
    public void ColumnaExtraConDatos_Rechaza()
    {
        using var libro = Libro();
        Poner(libro, 6, "Concepto", "Gasto");
        libro.GetSheet(PlantillaGastosDirectos.Hoja).GetRow(5).CreateCell(16).SetCellValue("123");
        Assert.Throws<EstructuraInvalidaException>(() => Leer(libro));
    }

    private static XSSFWorkbook Libro() => new(new MemoryStream(new ObtenerPlantillaGastosDirectosHandler().Handle()));

    private static ICell Poner(XSSFWorkbook libro, int fila, string campo, string texto)
    {
        var row = libro.GetSheet(PlantillaGastosDirectos.Hoja).GetRow(fila - 1);
        var celda = row.GetCell(PlantillaGastosDirectos.Encabezados.ToList().IndexOf(campo));
        celda.SetCellValue(texto);
        return celda;
    }

    private static List<SpreadsheetRow> Leer(XSSFWorkbook libro, string? numeroRaw = null)
    {
        using var stream = new MemoryStream();
        libro.Write(stream, true);
        if (numeroRaw is not null)
        {
            using var zip = new ZipArchive(stream, ZipArchiveMode.Update, true);
            var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open());
            writer.Write(xml.Replace("<v>3.76</v>", $"<v>{numeroRaw}</v>"));
        }
        var archivo = new FormFile(new MemoryStream(stream.ToArray()), 0, stream.Length, "archivo", "gastos.xlsx");
        return new ExcelFileParser().ParsePlantilla(archivo, PlantillaGastosDirectos.Hoja, 5,
            PlantillaGastosDirectos.Encabezados, PlantillaGastosDirectos.Auxiliares);
    }
}
