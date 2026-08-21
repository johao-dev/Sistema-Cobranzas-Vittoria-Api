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
/// <para>
/// <b>Algoritmo de importacion:</b>
/// <list type="number">
///   <item><b>Validacion de peso</b>: el archivo no debe exceder
///         <see cref="MaxBytesPorArchivo"/>; si lo excede, se lanza
///         <see cref="ArchivoInvalidoException"/> con codigo
///         <c>TAMANIO_EXCEDIDO</c> (mapea a HTTP 413).</item>
///   <item><b>Resolucion de parser</b>: se delega al
///         <see cref="FileParserResolver"/> para elegir entre CSV, XLSX o XLS
///         segun extension + magic numbers.</item>
///   <item><b>Parseo</b>: el parser convierte el <see cref="IFormFile"/> en
///         una lista de <see cref="SpreadsheetRow"/>.</item>
///   <item><b>Validacion de estructura</b>: presencia de los encabezados
///         requeridos declarados por la subclase.</item>
///   <item><b>Mapeo fila-a-DTO de archivo</b>: delega en
///         <see cref="MapearFila"/>, capturando excepciones de conversion y
///         traduciendolas a <see cref="DetalleErrorFila"/>.</item>
///   <item><b>Validacion de filas</b>: si se acumulo algun error, aborta con
///         <see cref="DatosInvalidosException"/> sin tocar la BD.</item>
///   <item><b>Construccion del TVP</b>: se delega en
///         <see cref="OnConstruirTvpAsync"/>. Para los 6 modulos sin logica
///         adicional, el default convierte archivo-a-TVP 1-a-1. Para Material
///         v2, la subclase override resuelve catalogos y mapea IDs.</item>
///   <item><b>Persistencia transaccional</b>: abre conexion + transaccion,
///         invoca el SP con el TVP, hace commit.</item>
///   <item><b>Traduccion de errores del SP</b>: si el SP lanza
///         <see cref="SqlException"/> 50001-50004 (validaciones de negocio),
///         se traduce a <see cref="DatosInvalidosException"/>.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Subclases concretas solo necesitan implementar:</b>
/// <list type="bullet">
///   <item><see cref="Modulo"/></item>
///   <item><see cref="SpName"/></item>
///   <item><see cref="TvpTypeName"/></item>
///   <item><see cref="EncabezadosRequeridos"/></item>
///   <item><see cref="MapearFila"/></item>
/// </list>
/// Si la transformacion archivo-a-TVP es trivial (mismo shape), no es
/// necesario override de <see cref="OnConstruirTvpAsync"/>: el comportamiento
/// default funciona (1-a-1 cast).
/// </para>
///
/// <typeparam name="TArchivo">
/// Tipo del DTO que representa una fila del archivo (ej:
/// <c>MaterialImportDto</c> con 4 campos string). Se usa en
/// <see cref="MapearFila"/>.
/// </typeparam>
/// <typeparam name="TTvp">
/// Tipo del DTO que se envia al TVP (ej: <c>MaterialImportTvpDto</c> con IDs
/// resueltos). Se usa al invocar el SP. Si el shape es identico a TArchivo,
/// la subclase puede usar el mismo tipo para ambos parametros genericos.
/// </typeparam>
/// </summary>
public abstract class ImportProcessorBase<TArchivo, TTvp> : IImportProcessor
    where TArchivo : class
    where TTvp : class
{
    /// <summary>
    /// Tamano maximo del archivo de importacion en bytes (5 MB). Reemplaza
    /// al antiguo limite de 100 filas porque el feature ahora soporta
    /// archivos grandes (decenas de miles de filas) siempre que el peso
    /// no exceda este umbral. Se valida en el processor (no en
    /// <c>FileValidator</c>) para mantener el control cerca de la logica
    /// de negocio.
    /// </summary>
    public const long MaxBytesPorArchivo = 5L * 1024L * 1024L;

    protected readonly FileParserResolver ParserResolver;
    protected readonly IImportRepository Repository;
    protected readonly IDbConnectionFactory ConnectionFactory;
    protected readonly ILogger Logger;

    protected ImportProcessorBase(
        FileParserResolver parserResolver,
        IImportRepository repository,
        IDbConnectionFactory connectionFactory,
        ILogger logger)
    {
        ParserResolver = parserResolver ?? throw new ArgumentNullException(nameof(parserResolver));
        Repository = repository ?? throw new ArgumentNullException(nameof(repository));
        ConnectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
    /// Convierte una fila del archivo en el DTO de importacion (forma
    /// "archivo"). Esta representacion puede no coincidir con la forma TVP:
    /// por ejemplo, Material v2 recibe strings (Especialidad, UnidadMedida)
    /// y luego se traducen a IDs antes de invocar el SP.
    ///
    /// Esta implementacion puede lanzar:
    ///   - <see cref="KeyNotFoundException"/>: si la columna requerida no existe o esta vacia.
    ///   - <see cref="FormatException"/>: si el valor no se puede convertir al tipo esperado.
    ///   - <see cref="DatosInvalidosException"/>: para reglas de negocio adicionales
    ///     que la subclase quiera validar a nivel de API (ej: codigo con prefijo invalido).
    ///
    /// Todas estas excepciones son capturadas por la base y traducidas a
    /// <see cref="DetalleErrorFila"/> con el numero de fila correspondiente.
    /// </summary>
    internal abstract TArchivo MapearFila(SpreadsheetRow fila);

    // ============================================================================
    // Hook de extension: archivo -> TVP
    // ============================================================================

    /// <summary>
    /// Hook opcional para que las subclases conviertan la lista de DTOs de
    /// archivo en la lista de DTOs de TVP. La implementacion default hace un
    /// cast 1-a-1 (util cuando <typeparamref name="TArchivo"/> y
    /// <typeparamref name="TTvp"/> son el mismo tipo, como en los 6 modulos
    /// que no necesitan transformacion).
    ///
    /// <para>
    /// Las subclases que SÍ necesitan transformacion (ej: Material v2,
    /// donde el archivo trae strings y el TVP espera IDs) override este
    /// metodo. La implementacion override se ejecuta DENTRO de la transaccion
    /// que abrio la base, lo cual permite que la subclase haga lecturas y
    /// escrituras atomicas con la insercion final del SP.
    /// </para>
    /// </summary>
    /// <param name="archivos">DTOs validados de la plantilla del usuario.</param>
    /// <param name="cn">Conexion abierta de la transaccion en curso.</param>
    /// <param name="tx">Transaccion en curso (la misma que se usara para el SP).</param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <returns>Lista de DTOs con la forma que espera el TVP.</returns>
    protected virtual Task<IReadOnlyList<TTvp>> OnConstruirTvpAsync(
        IReadOnlyList<TArchivo> archivos,
        IDbConnection cn,
        IDbTransaction tx,
        CancellationToken ct)
    {
        // Default 1-a-1: solo funciona si TArchivo y TTvp son el mismo tipo.
        // La conversion se hace via Cast<TTvp> que fallara en runtime si los
        // tipos no son compatibles. Es una garantia estatica: si el processor
        // declara ImportProcessorBase<MaterialImportDto, MaterialImportDto>,
        // el cast funciona; si declara tipos distintos, DEBE override este
        // metodo.
        var tvps = archivos.Cast<TTvp>().ToList();
        return Task.FromResult<IReadOnlyList<TTvp>>(tvps);
    }

    // ============================================================================
    // Template Method
    // ============================================================================

    public async Task<ResultadoImportacion> EjecutarAsync(IFormFile file, string usuario, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrEmpty(usuario);

        // 0. Validar peso del archivo. Reemplazo al antiguo limite de 100 filas.
        //    Se valida aqui (no en FileValidator) porque el limite es propio de
        //    la logica de importacion (otros endpoints pueden tolerar archivos
        //    mas grandes). El codigo "TAMANIO_EXCEDIDO" ya esta mapeado a
        //    HTTP 413 en el ApiExceptionMiddleware.
        if (file.Length > MaxBytesPorArchivo)
        {
            var mb = file.Length / 1024d / 1024d;
            Logger.LogWarning(
                "[{Modulo}] Archivo rechazado por peso. Tamano={Tamano:F2}MB Maximo={Maximo}MB",
                Modulo, mb, MaxBytesPorArchivo / 1024d / 1024d);
            throw new ArchivoInvalidoException(
                "TAMANIO_EXCEDIDO",
                $"El archivo supera el tamano maximo permitido de {MaxBytesPorArchivo / 1024 / 1024} MB. Tamano actual: {mb:F2} MB.");
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        Logger.LogInformation(
            "[{Modulo}] Iniciando importacion. Archivo={FileName} Tamano={Tamano}B Usuario={Usuario}",
            Modulo, file.FileName, file.Length, usuario);

        // 1-2. Resolucion de parser + parseo
        var parser = ParserResolver.ObtenerParser(file);
        var filas = parser.Parse(file);
        // El formato del ResultadoImportacion se calcula a partir de la
        // extension real del archivo (no de parser.Formato) porque
        // ExcelFileParser.Formato es "xlsx/xls" (cubre ambos formatos con
        // el mismo parser); queremos que el campo "formato" de la respuesta
        // sea "xlsx" o "xls" segun corresponda.
        var formatoRespuesta = InferirFormatoDesdeExtension(file.FileName, parser.Formato);
        Logger.LogDebug(
            "[{Modulo}] Parser={Formato} produjo {CantidadFilas} filas",
            Modulo, parser.Formato, filas.Count);

        // 3. Validacion de estructura
        ValidarEstructura(filas);
        Logger.LogDebug(
            "[{Modulo}] Estructura validada: {CantidadFilas} filas, encabezados OK",
            Modulo, filas.Count);

        // 4-5. Mapeo + validacion de filas (forma "archivo")
        var archivos = new List<TArchivo>(filas.Count);
        var errores = new List<DetalleErrorFila>();
        MapearFilas(filas, archivos, errores);

        if (errores.Count > 0)
        {
            var codigosUnicos = errores
                .GroupBy(e => e.CodigoError)
                .Select(g => $"{g.Key}={g.Count()}")
                .OrderBy(s => s);
            Logger.LogWarning(
                "[{Modulo}] Rechazado por validacion de filas. TotalErrores={Total} Codigos=[{Codigos}]",
                Modulo, errores.Count, string.Join(", ", codigosUnicos));
            throw new DatosInvalidosException(
                $"El archivo contiene {errores.Count} fila(s) con errores. No se realizo ninguna insercion.",
                errores);
        }

        Logger.LogDebug(
            "[{Modulo}] Mapeo completado: {CantidadArchivos} DTOs de archivo sin errores",
            Modulo, archivos.Count);

        // 6-8. Construccion del TVP + persistencia transaccional
        var filasInsertadas = await EjecutarCargaAsync(archivos, usuario, ct);

        sw.Stop();
        Logger.LogInformation(
            "[{Modulo}] Importacion exitosa. FilasInsertadas={Filas} Duracion={Duracion}ms",
            Modulo, filasInsertadas, sw.ElapsedMilliseconds);

        return new ResultadoImportacion(Modulo, formatoRespuesta, filasInsertadas);
    }

    /// <summary>
    /// Normaliza el formato reportado al cliente HTTP a partir de la extension
    /// del archivo. Se usa porque <c>ExcelFileParser.Formato</c> cubre dos
    /// formatos con un unico parser (devuelve "xlsx/xls"); queremos que el
    /// campo <c>formato</c> del <see cref="ResultadoImportacion"/> refleje
    /// la extension real (.xlsx o .xls).
    /// </summary>
    private static string InferirFormatoDesdeExtension(string fileName, string parserFormato)
    {
        var ext = Path.GetExtension(fileName)?.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "csv"   => "csv",
            "xlsx"  => "xlsx",
            "xls"   => "xls",
            _       => parserFormato
        };
    }

    // ============================================================================
    // Pasos del template (protegidos para que las subclases puedan customizarlos)
    // ============================================================================

    /// <summary>
    /// Valida los encabezados requeridos. Lanza
    /// <see cref="DatosInvalidosException"/> si el archivo esta vacio o
    /// <see cref="EstructuraInvalidaException"/> si faltan encabezados.
    ///
    /// <para>
    /// A diferencia de la v1, esta validacion NO incluye un limite de filas
    /// (eliminado al introducir el limite de peso). Los archivos pueden
    /// contener cualquier cantidad de filas; el cuello de botella es el
    /// peso (5 MB), no la cantidad.
    /// </para>
    /// </summary>
    protected virtual void ValidarEstructura(IReadOnlyList<SpreadsheetRow> filas)
    {
        if (filas.Count == 0)
        {
            throw new DatosInvalidosException(
                "El archivo no contiene filas de datos (solo encabezados o esta vacio).",
                Array.Empty<DetalleErrorFila>());
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
    /// Mapea cada fila al DTO de archivo. Los errores de conversion se acumulan
    /// en <paramref name="errores"/> en lugar de abortar el mapeo completo: asi
    /// el usuario recibe TODOS los errores en una sola respuesta 422.
    /// </summary>
    protected virtual void MapearFilas(
        IReadOnlyList<SpreadsheetRow> filas,
        List<TArchivo> archivos,
        List<DetalleErrorFila> errores)
    {
        foreach (var fila in filas)
        {
            try
            {
                var dto = MapearFila(fila);
                archivos.Add(dto);
            }
            catch (DatosInvalidosException dex) when (dex.Errores.Count > 0)
            {
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
    /// Abre la conexion, inicia la transaccion, delega en
    /// <see cref="OnConstruirTvpAsync"/> para construir el TVP, invoca el SP
    /// y commitea. Si el SP lanza <see cref="SqlException"/> 50001-50004, se
    /// traduce a <see cref="DatosInvalidosException"/> con el mensaje original.
    ///
    /// <para>
    /// Se hizo PRIVATE de nuevo (igual que en v1) para que las subclases NO
    /// puedan intervenir en la transaccion. Las subclases que necesitan logica
    /// adicional (resolucion de catalogos, calculos derivados) override
    /// <see cref="OnConstruirTvpAsync"/>, que se invoca DENTRO de la
    /// transaccion que abrimos aca. Asi el patron Template Method sigue
    /// rigiendo el flujo transaccional.
    /// </para>
    /// </summary>
    private async Task<int> EjecutarCargaAsync(
        IReadOnlyList<TArchivo> archivos, string usuario, CancellationToken ct)
    {
        IDbConnection? connection = null;
        IDbTransaction? transaction = null;
        try
        {
            connection = ConnectionFactory.CreateConnection();
            if (connection.State != ConnectionState.Open)
            {
                if (connection is SqlConnection sqlConn)
                {
                    await sqlConn.OpenAsync(ct);
                }
                else
                {
                    connection.Open();
                }
            }

            transaction = connection.BeginTransaction();
            Logger.LogDebug(
                "[{Modulo}] Conexion abierta y transaccion iniciada. SP={Sp} TVP={Tvp} Filas={Filas}",
                Modulo, SpName, TvpTypeName, archivos.Count);

            // Construir el TVP dentro de la transaccion. Esto permite que la
            // subclase (si override OnConstruirTvpAsync) haga lecturas y
            // escrituras atomicas con la insercion final del SP.
            IReadOnlyList<TTvp> tvps;
            try
            {
                tvps = await OnConstruirTvpAsync(archivos, connection, transaction, ct);
            }
            catch (Exception ex) when (ex is not DatosInvalidosException)
            {
                // Si la transformacion archivo -> TVP falla (ej: no se pudo
                // resolver un catalogo), hacemos rollback y relanzamos.
                transaction.Rollback();
                Logger.LogError(ex,
                    "[{Modulo}] Fallo la construccion del TVP. TipoError={TipoError}",
                    Modulo, ex.GetType().Name);
                throw;
            }

            if (tvps.Count == 0)
            {
                // Sin filas para TVP (no deberia pasar porque ValidarEstructura
                // ya rechazo el caso vacio, pero defendemos en profundidad).
                transaction.Rollback();
                Logger.LogWarning(
                    "[{Modulo}] OnConstruirTvpAsync devolvio 0 filas; no se invoca el SP.",
                    Modulo);
                return 0;
            }

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var count = await Repository.ImportAsync(
                    SpName, TvpTypeName, tvps, connection, transaction,
                    new { Usuario = usuario }, ct);
                sw.Stop();
                Logger.LogDebug(
                    "[{Modulo}] SP ejecutado en {Duracion}ms. FilasAfectadas={Filas}",
                    Modulo, sw.ElapsedMilliseconds, count);

                transaction.Commit();
                Logger.LogDebug("[{Modulo}] Transaccion confirmada (commit).", Modulo);
                return count;
            }
            catch (SqlException ex) when (ex.Number is >= 50001 and <= 50099)
            {
                transaction.Rollback();
                Logger.LogWarning(
                    "[{Modulo}] SP rechazo la carga (SqlException {Numero} -> {CodigoError}): {Mensaje}",
                    Modulo, ex.Number, MapearCodigoSql(ex.Number), ex.Message);
                throw TraducirSqlException(ex);
            }
            catch (SqlException ex)
            {
                transaction.Rollback();
                Logger.LogError(ex,
                    "[{Modulo}] Error de SQL no esperado (Numero={Numero}). Transaccion revertida.",
                    Modulo, ex.Number);
                throw;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                Logger.LogError(ex,
                    "[{Modulo}] Error inesperado durante la carga. Transaccion revertida. Tipo={TipoError}",
                    Modulo, ex.GetType().Name);
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
    /// <see cref="DatosInvalidosException"/>.
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
