namespace Cobranzas_Vittoria.Application.Importacion.Processors;

/// <summary>
/// Contrato del patron Template Method aplicado a la importacion masiva.
///
/// Cada modulo de mantenimiento (UnidadMedida, Especialidad, Material, etc.)
/// tiene una implementacion concreta de <see cref="IImportProcessor"/> que
/// encapsula las particularidades del mapeo fila-a-DTO y de los nombres del
/// Stored Procedure / TVP. La clase base <see cref="ImportProcessorBase{TDto}"/>
/// contiene el algoritmo comun (parseo, validacion de estructura, transaccion,
/// traduccion de excepciones).
///
/// La resolucion del processor adecuado para un modulo se hace en el service
/// o el controller usando <c>Microsoft.Extensions.DependencyInjection</c> con
/// keyed services (de .NET 8), indexados por <see cref="Modulo"/>.
/// </summary>
public interface IImportProcessor
{
    /// <summary>
    /// Identificador unico del modulo. Es el segmento de URL del endpoint
    /// POST <c>/api/import/{Modulo}</c>. Se usa como clave para resolver
    /// el processor concreto desde el container de DI.
    /// </summary>
    string Modulo { get; }

    /// <summary>
    /// Ejecuta la importacion del archivo sobre la base de datos.
    ///
    /// El algoritmo comun (en la clase base) hace, en orden:
    ///   1. Resuelve el parser adecuado (CSV/XLSX/XLS) segun extension y magic numbers.
    ///   2. Parsea el archivo a una lista de <c>SpreadsheetRow</c>.
    ///   3. Valida la estructura (encabezados requeridos, cantidad maxima de filas).
    ///   4. Mapea cada fila al DTO correspondiente (metodo abstracto de la subclase),
    ///      acumulando errores por fila en una lista.
    ///   5. Si hay errores de mapeo, lanza <c>DatosInvalidosException</c> con
    ///      el detalle y aborta sin tocar la BD.
    ///   6. Abre una transaccion, invoca el SP de carga masiva con el TVP.
    ///   7. Si el SP lanza <c>SqlException</c> 50001-50004, traduce a
    ///      <c>DatosInvalidosException</c> con el mensaje original.
    ///   8. Commit y retorno del <see cref="ResultadoImportacion"/>.
    /// </summary>
    /// <param name="file">Archivo enviado en el request HTTP.</param>
    /// <param name="usuario">Identificador del usuario que ejecuta la importacion (para @Usuario del SP).</param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <exception cref="Excepciones.ArchivoInvalidoException">
    ///     Extension/MIME/tamanio invalido. HTTP 400 o 413.
    /// </exception>
    /// <exception cref="Excepciones.EstructuraInvalidaException">
    ///     Encoding, delimitador, encabezados faltantes. HTTP 400.
    /// </exception>
    /// <exception cref="Excepciones.DatosInvalidosException">
    ///     Fila con tipo/formato/regla de negocio invalida, o error del SP. HTTP 422.
    /// </exception>
    Task<ResultadoImportacion> EjecutarAsync(IFormFile file, string usuario, CancellationToken ct);
}
