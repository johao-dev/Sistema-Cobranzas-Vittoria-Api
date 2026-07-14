using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Validators;
using Cobranzas_Vittoria.Tests.Unit.Importacion.Common;

namespace Cobranzas_Vittoria.Tests.Unit.Importacion;

/// <summary>
/// Pruebas unitarias de <see cref="FileValidator"/>.
/// Cubre las reglas de validacion del archivo: vacio, extension, tamano, MIME.
/// </summary>
public class FileValidatorTests
{
    private readonly FileValidator _sut = new();

    [Test]
    public void Validar_ArchivoNulo_LanzaArchivoInvalidoVacio()
    {
        var ex = Assert.Throws<ArchivoInvalidoException>(() => _sut.Validar(null!))!;
        Assert.That(ex.Codigo, Is.EqualTo("ARCHIVO_VACIO"));
    }

    [Test]
    public void Validar_ArchivoVacio_LanzaArchivoInvalidoVacio()
    {
        var archivo = TestFormFiles.FromBytes(Array.Empty<byte>(), "datos.csv", "text/csv");
        var ex = Assert.Throws<ArchivoInvalidoException>(() => _sut.Validar(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("ARCHIVO_VACIO"));
    }

    [Test]
    public void Validar_ExtensionTxt_LanzaExtensionInvalida()
    {
        var archivo = TestFormFiles.FromText("hola", "datos.txt", "text/plain");
        var ex = Assert.Throws<ArchivoInvalidoException>(() => _sut.Validar(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("EXTENSION_INVALIDA"));
        Assert.That(ex.Message, Does.Contain(".csv"));
        Assert.That(ex.Message, Does.Contain(".xlsx"));
    }

    [Test]
    public void Validar_ExtensionCsvValida_NoLanza()
    {
        var archivo = TestFormFiles.FromText("a,b\n1,2", "datos.csv", "text/csv");
        Assert.DoesNotThrow(() => _sut.Validar(archivo));
    }

    [Test]
    public void Validar_ExtensionXlsxValida_NoLanza()
    {
        // 1 KB de bytes cualquiera (magic number check se hace en el parser, no en el validator)
        var archivo = TestFormFiles.FromBytes(new byte[1024], "datos.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        Assert.DoesNotThrow(() => _sut.Validar(archivo));
    }

    [Test]
    public void Validar_ExtensionXlsValida_NoLanza()
    {
        var archivo = TestFormFiles.FromBytes(new byte[1024], "datos.xls", "application/vnd.ms-excel");
        Assert.DoesNotThrow(() => _sut.Validar(archivo));
    }

    [Test]
    public void Validar_ArchivoDe11MB_LanzaTamanioExcedido()
    {
        // 11 MB
        var bytes = new byte[11 * 1024 * 1024];
        var archivo = TestFormFiles.FromBytes(bytes, "datos.csv", "text/csv");
        var ex = Assert.Throws<ArchivoInvalidoException>(() => _sut.Validar(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("TAMANIO_EXCEDIDO"));
    }

    [Test]
    public void Validar_ArchivoDe10MBExacto_NoLanza()
    {
        // 10 MB exactos es el limite; debe pasar.
        var bytes = new byte[10 * 1024 * 1024];
        var archivo = TestFormFiles.FromBytes(bytes, "datos.csv", "text/csv");
        Assert.DoesNotThrow(() => _sut.Validar(archivo));
    }

    [Test]
    public void Validar_MimeInconsistenteConExtension_LanzaMimeInvalido()
    {
        // .xlsx declarado pero con MIME de imagen
        var archivo = TestFormFiles.FromBytes(new byte[1024], "datos.xlsx", "image/png");
        var ex = Assert.Throws<ArchivoInvalidoException>(() => _sut.Validar(archivo))!;
        Assert.That(ex.Codigo, Is.EqualTo("MIME_INVALIDO"));
    }

    [Test]
    public void Validar_MimeOctetStream_EsAceptado()
    {
        // application/octet-stream se acepta siempre (MIME "generico" que algunos browsers envian).
        var archivo = TestFormFiles.FromBytes(new byte[1024], "datos.xlsx", "application/octet-stream");
        Assert.DoesNotThrow(() => _sut.Validar(archivo));
    }

    [Test]
    public void Validar_MimeVacio_EsAceptado()
    {
        // Sin Content-Type (algunos clientes HTTP no lo envian).
        var archivo = TestFormFiles.FromBytes(new byte[1024], "datos.csv", "");
        Assert.DoesNotThrow(() => _sut.Validar(archivo));
    }
}
