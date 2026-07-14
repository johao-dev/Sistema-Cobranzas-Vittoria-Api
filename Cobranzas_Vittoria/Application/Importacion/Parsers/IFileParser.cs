using Cobranzas_Vittoria.Domain.Importacion;

namespace Cobranzas_Vittoria.Application.Importacion.Parsers;

/// <summary>
/// Contrato para los parsers de archivos de importacion masiva.
/// Cada implementacion conoce un formato (CSV, XLSX, XLS) y sabe como
/// convertir un <see cref="IFormFile"/> en una lista de <see cref="SpreadsheetRow"/>.
///
/// La deteccion del formato se hace en dos niveles:
///   - <see cref="PuedeParsear"/> recibe la extension del archivo y los primeros bytes
///     para verificar tanto la extension declarada como los "magic numbers" del contenido.
///     Esto evita que un .csv que en realidad es un binario termine en el parser de CSV.
///   - <see cref="Parse"/> realiza la conversion real. Si la estructura del archivo es
///     invalida (encoding incorrecto, delimitador no es ",", archivo corrupto), lanza
///     <c>EstructuraInvalidaException</c> con el codigo apropiado.
/// </summary>
public interface IFileParser
{
    /// <summary>
    /// Identificador del formato ("csv", "xlsx", "xls"). Util para logging y diagnostico.
    /// </summary>
    string Formato { get; }

    /// <summary>
    /// Determina si este parser puede procesar el archivo basandose en la extension
    /// y los primeros bytes (magic numbers). No lee el archivo completo.
    /// </summary>
    /// <param name="extension">Extension del archivo en lowercase, con punto (ej: ".csv").</param>
    /// <param name="primerosBytes">
    /// Primeros bytes del archivo. Para CSV se esperan 4+ bytes que permitan
    /// detectar BOM UTF-8 o ausencia de binarios. Para Excel se esperan los
    /// 8 bytes que componen la firma OLE2 (.xls) o ZIP (.xlsx).
    /// </param>
    bool PuedeParsear(string extension, ReadOnlySpan<byte> primerosBytes);

    /// <summary>
    /// Lee el archivo y lo convierte en filas normalizadas.
    /// </summary>
    /// <exception cref="Cobranzas_Vittoria.Application.Importacion.Excepciones.EstructuraInvalidaException">
    /// Si la estructura del archivo es invalida (encoding, delimitador, formato binario corrupto, etc.).
    /// </exception>
    List<SpreadsheetRow> Parse(IFormFile file);
}
