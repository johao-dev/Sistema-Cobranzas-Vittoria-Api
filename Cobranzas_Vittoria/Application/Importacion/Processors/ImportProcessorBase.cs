using System.Data;
using Cobranzas_Vittoria.Application.Importacion.Excepciones;
using Cobranzas_Vittoria.Application.Importacion.Parsers;
using Cobranzas_Vittoria.Application.Importacion.Persistence;
using Cobranzas_Vittoria.Data;
using Cobranzas_Vittoria.Domain.Importacion;
using Microsoft.Data.SqlClient;

namespace Cobranzas_Vittoria.Application.Importacion.Processors;

/// <summary>
/// Clase base abstracta que implementa el patron Template Method para la
/// importacion masiva.
///
/// El algoritmo de importacion esta definido aqui y es invariable:
///   1. <b>Resolucion del parser</b>: se delega al <see cref="FileParserResolver"/>
///      para elegir entre CSV, XLSX o XLS segun extension + magic numbers.
///   2. <b>Parseo</b>: el parser convierte el <see cref="IFormFile"/> en una lista
///      de <see cref="SpreadsheetRow"/>.
///   3. <b>Validacion de estructura</b>: cantidad de filas dentro del limite,
///      y presencia de los encabezados requeridos declarados por la subclase.
///   4. <b>Mapeo fila-a-DTO</b>: delega en <see cref="MapearFila"/>, capturando
///      excepciones de conversion (FormatException, KeyNotFoundException) y
///      traduciendolas a <see cref="DetalleErrorFila"/>.
///   5. <b>Validacion de filas</b>: si se acumulo algun error, aborta con
///      <see cref="DatosInvalidosException"/> sin tocar la BD.
///   6. <b>Persistencia transaccional</b>: abre conexion + transaccion,
///      invoca el SP con el TVP, hace commit.
///   7. <b>Traduccion de errores del SP</b>: si el SP lanza SqlException con
///      numero 50001-50004 (validaciones de negocio), se traduce a
///      <see cref="DatosInvalidosException"/> para mantener un unico contrato HTTP.
///
/// Las subclases concretas solo necesitan implementar:
///   - <see cref="Modulo"/>
///   - <see cref="SpName"/>
///   - <see cref="TvpTypeName"/>
///   - <see cref="EncabezadosRequeridos"/>
///   - <see cref="MapearFila"/>
/// </summary>
/// <typeparam name="TDto">
/// Tipo del DTO de importacion (ej: <c>UnidadMedidaImportDto</c>).
/// Debe ser una clase con propiedades publicas en el mismo orden que el TVP
/// (requerido por <see cref="TvpMapper"/>).
/// </typeparam>
public abstract class ImportProcessorBase<TDto> : IImportProcessor where TDto : class
{
    /// <summary>Limite maximo de filas que puede contener un archivo de importacion.</summary>
    public const int MaxFilasPorArchivo = 100;

    protected readonly FileParserResolver ParserResolver;
    protected readonly IImportRepository Repository;
    protected readonly IDbConnectionFactory ConnectionFactory;

    protected ImportProcessorBase(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory)
    {
        ParserResolver = parserResolver ?? throw new ArgumentNullException(nameof(parserResolver));
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        ConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    // ============================================================================
    // Propiedades que las subclases DEBEN implementar
    // ============================================================================

    /// <inheritdoc />
    public abstract string Modulo { get; }

    /// <summary>Nombre completo del Stored Procedure a invocar (ej: "maestra.usp_UnidadMedida_CargaMasiva").</summary>
    protected abstract string SpName { get; }

    /// <summary>Nombre completo del TVP (ej: "maestra.TVP_UnidadMedida").</summary>
    protected abstract string TvpTypeName { get; }

    /// <summary>
    /// Nombres de las columnas que el archivo DEBE contener. La validacion es
    /// case-insensitive (OrdinalIgnoreCase). Si falta alguna, se lanza
    /// <see cref="EstructuraInvalidaException"/> con codigo "ENCABEZADOS_INCORRECTOS".
    /// </summary>
    protected abstract string[] EncabezadosRequeridos { get; }

    /// <summary>
    /// Convierte una fila del archivo en el DTO de importacion.
    ///
    /// Esta implementacion puede lanzar:
    ///   - <see cref="KeyNotFoundException"/>: si la columna requerida no existe o esta vacia.
    ///   - <see cref="FormatException"/>: si el valor no se puede convertir al tipo esperado.
    ///   - <see cref="DatosInvalidosException"/>: para reglas de negocio adicionales
    ///     que la subclase quiera validar a nivel de API (ej: codigo con prefijo invalido).
    ///
    /// Todas estas excepciones son capturadas por la base y traducidas a
    /// <see cref="DetalleErrorFila"/> con el numero de fila correspondiente.
    ///
    /// Se declara <c>internal</c> (no <c>protected</c>) para que el proyecto de
    /// tests pueda invocarlo directamente sin reflection ni <c>dynamic</c>. La
    /// visibilidad externa de la API no cambia porque sigue siendo accesible
    /// solo desde el mismo assembly o, via <c>InternalsVisibleTo</c>, desde
    /// el assembly de tests.
    /// </summary>
    internal abstract TDto MapearFila(SpreadsheetRow fila);

    // ============================================================================
    // Template Method
    // ============================================================================

    public async Task<ResultadoImportacion> EjecutarAsync(IFormFile file, string usuario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrEmpty(usuario);

        // 1-2. Resolucion de parser + parseo
        var parser = ParserResolver.ObtenerParser(file);
        var filas = parser.Parse(file);

        // 3. Validacion de estructura
        ValidarEstructura(filas);

        // 4-5. Mapeo + validacion de filas
        var dtos = new List<TDto>(filas.Count);
        var errores = new List<DetalleErrorFila>();
        MapearFilas(filas, dtos, errores);

        if (errores.Count > 0)
        {
            throw new DatosInvalidosException(
                $"El archivo contiene {errores.Count} fila(s) con errores. No se realizo ninguna insercion.",
                errores);
        }

        // 6-7. Persistencia transaccional
        var filasInsertadas = await EjecutarCargaAsync(dtos, usuario, ct);

        return new ResultadoImportacion(Modulo, parser.Formato, filasInsertadas);
    }

    // ============================================================================
    // Pasos del template (protegidos para que las subclases puedan customizarlos)
    // ============================================================================

    /// <summary>
    /// Valida la cantidad de filas y los encabezados requeridos. Lanza
    /// <see cref="DatosInvalidosException"/> si el archivo esta vacio o
    /// <see cref="EstructuraInvalidaException"/> si faltan encabezados.
    /// </summary>
    protected virtual void ValidarEstructura(IReadOnlyList<SpreadsheetRow> filas)
    {
        if (filas.Count == 0)
        {
            throw new DatosInvalidosException(
                "El archivo no contiene filas de datos (solo encabezados o esta vacio).",
                Array.Empty<DetalleErrorFila>());
        }

        if (filas.Count > MaxFilasPorArchivo)
        {
            throw new DatosInvalidosException(
                $"El archivo contiene {filas.Count} filas, excede el maximo permitido de {MaxFilasPorArchivo}.",
                new[]
                {
                    new DetalleErrorFila(0, string.Empty, CodigosError.Estructura.DemasiadasFilas,
                        $"El maximo es {MaxFilasPorArchivo} filas por archivo.")
                });
        }

        // Tomamos los encabezados de la primera fila del archivo.
        // Como todos los parsers (CSV y Excel) exponen las columnas por nombre,
        // usamos la primera fila para validar.
        var headersArchivo = filas[0].Columnas
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var faltantes = EncabezadosRequeridos
            .Where(req => !headersArchivo.Contains(req))
            .ToArray();

        if (faltantes.Length > 0)
        {
            throw new EstructuraInvalidaException(
                CodigosError.Estructura.EncabezadosIncorrectos,
                $"Faltan las siguientes columnas requeridas: {string.Join(", ", faltantes)}. " +
                $"Encabezados recibidos: {string.Join(", ", headersArchivo)}.");
        }
    }

    /// <summary>
    /// Mapea cada fila al DTO. Los errores de conversion se acumulan en
    /// <paramref name="errores"/> en lugar de abortar el mapeo completo: asi
    /// el usuario recibe TODOS los errores en una sola respuesta 422.
    /// </summary>
    protected virtual void MapearFilas(
        IReadOnlyList<SpreadsheetRow> filas,
        List<TDto> dtos,
        List<DetalleErrorFila> errores)
    {
        foreach (var fila in filas)
        {
            try
            {
                var dto = MapearFila(fila);
                dtos.Add(dto);
            }
            catch (DatosInvalidosException dex) when (dex.Errores.Count > 0)
            {
                // La subclase ya construyo un DetalleErrorFila con contexto
                // (fila, campo, mensaje). Lo agregamos a la coleccion global.
                errores.AddRange(dex.Errores);
            }
            catch (KeyNotFoundException ex)
            {
                errores.Add(new DetalleErrorFila(
                    fila.NumeroFila, string.Empty, CodigosError.Fila.CampoRequerido, ex.Message));
            }
            catch (FormatException ex)
            {
                errores.Add(new DetalleErrorFila(
                    fila.NumeroFila, string.Empty, CodigosError.Fila.FormatoInvalido, ex.Message));
            }
            catch (InvalidCastException ex)
            {
                errores.Add(new DetalleErrorFila(
                    fila.NumeroFila, string.Empty, CodigosError.Fila.FormatoInvalido, ex.Message));
            }
        }
    }

    /// <summary>
    /// Abre la conexion, inicia la transaccion, invoca el SP y commitea.
    /// Si el SP lanza <see cref="SqlException"/> 50001-50004, se traduce a
    /// <see cref="DatosInvalidosException"/> con el mensaje original.
    /// </summary>
    private async Task<int> EjecutarCargaAsync(
        IReadOnlyList<TDto> dtos, string usuario, CancellationToken ct)
    {
        IDbConnection? connection = null;
        IDbTransaction? transaction = null;
        try
        {
            connection = ConnectionFactory.CreateConnection();
            if (connection is SqlConnection sqlConn)
            {
                await sqlConn.OpenAsync(ct);
            }
            else
            {
                connection.Open();
            }

            transaction = connection.BeginTransaction();

            try
            {
                var count = await Repository.ImportAsync(
                    SpName, TvpTypeName, dtos, connection, transaction,
                    new { Usuario = usuario }, ct);

                transaction.Commit();
                return count;
            }
            catch (SqlException ex) when (ex.Number is >= 50001 and <= 50099)
            {
                transaction.Rollback();
                throw TraducirSqlException(ex);
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        finally
        {
            transaction?.Dispose();
            if (connection is not null)
            {
                if (connection is SqlConnection sc) await sc.CloseAsync();
                else connection.Close();
                connection.Dispose();
            }
        }
    }

    /// <summary>
    /// Traduce una <see cref="SqlException"/> lanzada por el SP a una
    /// <see cref="DatosInvalidosException"/>. Por ahora la fila reportada es 0
    /// (no se incluye en el THROW); mejorar el SP para emitir la fila y
    /// parsearla aqui es una mejora futura.
    /// </summary>
    internal static DatosInvalidosException TraducirSqlException(SqlException ex)
    {
        var detalle = new DetalleErrorFila(
            Fila: 0,
            Campo: string.Empty,
            CodigoError: MapearCodigoSql(ex.Number),
            Mensaje: ex.Message);

        return new DatosInvalidosException(
            $"El Stored Procedure rechazo la carga: {ex.Message}",
            new[] { detalle });
    }

    /// <summary>
    /// Mapea el numero de error del SP a un codigo de error legible.
    /// Extraido de <see cref="TraducirSqlException"/> para poder testearlo
    /// sin necesidad de instanciar un <see cref="SqlException"/> (que es sealed
    /// y no tiene constructor publico).
    /// Se declara <c>internal</c> (no <c>public</c>) para que solo lo consuma
    /// el processor y el proyecto de tests.
    /// </summary>
    internal static string MapearCodigoSql(int numero)
    {
        return numero switch
        {
            50001 => CodigosError.Sp.CampoObligatorio,
            50002 => CodigosError.Sp.ValorDuplicadoEnArchivo,
            50003 => CodigosError.Sp.ValorYaExisteEnBd,
            50004 => CodigosError.Sp.FkNoExiste,
            _ => $"{CodigosError.Sp.ErrorValidacionPrefijo}_{numero}"
        };
    }

    // ============================================================================
    // Helpers de mapeo reutilizables para las subclases.
    //
    // Centralizan la logica repetida de "columna opcional con default" que
    // antes duplicaban los 7 processors: tres lineas de ContieneColumna +
    // TryGetString + TryGetT para parsear un valor que puede estar ausente.
    // Lanzan <see cref="FormatException"/> (mapeado por la base a
    // CodigosError.Fila.FormatoInvalido) si la columna existe pero el valor
    // no es parseable.
    // ============================================================================

    /// <summary>
    /// Lee una columna booleana opcional. Si la columna no existe o esta vacia,
    /// devuelve <paramref name="defaultValue"/>. Si existe pero el valor no
    /// es booleano valido, lanza <see cref="FormatException"/>.
    /// </summary>
    protected static bool LeerBoolConDefault(SpreadsheetRow fila, string columna, bool defaultValue)
    {
        if (!fila.ContieneColumna(columna) || !fila.TryGetString(columna, out var raw) || raw is null)
            return defaultValue;

        if (!fila.TryGetBool(columna, out var value))
            throw new FormatException($"La columna '{columna}' contiene un valor booleano invalido: '{raw}'.");
        return value;
    }

    /// <summary>
    /// Lee una columna entera opcional. Si la columna no existe o esta vacia,
    /// devuelve <c>null</c>. Si existe pero el valor no es entero valido,
    /// lanza <see cref="FormatException"/>.
    /// </summary>
    protected static int? LeerIntNullable(SpreadsheetRow fila, string columna)
    {
        if (!fila.ContieneColumna(columna) || !fila.TryGetString(columna, out var raw) || raw is null)
            return null;

        if (!fila.TryGetInt32(columna, out var value))
            throw new FormatException($"La columna '{columna}' no es un entero valido: '{raw}'.");
        return value;
    }

    /// <summary>
    /// Lee una columna decimal opcional. Si la columna no existe o esta vacia,
    /// devuelve <paramref name="defaultValue"/>. Si existe pero el valor no
    /// es decimal valido, lanza <see cref="FormatException"/>.
    /// </summary>
    protected static decimal LeerDecimalConDefault(SpreadsheetRow fila, string columna, decimal defaultValue)
    {
        if (!fila.ContieneColumna(columna) || !fila.TryGetString(columna, out var raw) || raw is null)
            return defaultValue;

        if (!fila.TryGetDecimal(columna, out var value))
            throw new FormatException($"La columna '{columna}' no es un decimal valido: '{raw}'.");
        return value;
    }
}
