using Cobranzas_Vittoria.Application.Common;
using Cobranzas_Vittoria.Application.Common.Excepciones;
using Cobranzas_Vittoria.Application.Common.Exports;
using Cobranzas_Vittoria.Application.Inventario.Dtos;
using Cobranzas_Vittoria.Application.Inventario.Excepciones;
using Cobranzas_Vittoria.Application.Inventario.Exports;
using Cobranzas_Vittoria.Application.Inventario.Persistence;
using Cobranzas_Vittoria.Application.Inventario.Validators;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Cobranzas_Vittoria.Application.Inventario.Services;

/// <summary>
/// Implementacion de <see cref="IKardexInventarioService"/>.
///
/// <para>
/// <b>Pipeline de una operacion tipica</b> (ej: POST /entradas):
///   1. <see cref="KardexInventarioValidator"/> valida el DTO.
///   2. <see cref="IKardexEntradaRepository"/> ejecuta el SP.
///   3. Si el SP lanza <see cref="SqlException"/> 51100-51199,
///      <see cref="SqlExceptionTranslator"/> la traduce a un
///      <see cref="ResultadoTraduccionSql"/>.
///   4. El service envuelve el resultado en
///      <see cref="ValidacionNegocioInventarioException"/> (que extiende
///      <see cref="DatosInvalidosValidacionException"/>) para que el
///      <c>ApiExceptionMiddleware</c> la mapee a HTTP 422.
/// </para>
///
/// <para>
/// <b>Por que NO se traduce en el repositorio</b>:
/// la traduccion es una decision de la capa de aplicacion, no de
/// persistencia. El repositorio solo conoce SPs y DTOs; lanzarle
/// una <c>SqlException</c> cruda al service lo obliga a decidir el
/// formato de la respuesta HTTP, que es su responsabilidad.
/// </para>
///
/// <para>
/// <b>Por que <c>IDbConnectionFactory</c> no se inyecta aqui</b>:
/// la transaccion la controla el SP (<c>SET XACT_ABORT ON</c> +
/// <c>BEGIN TRAN</c> interno). El service no necesita abrir conexiones
/// ni gestionar transacciones, asi que no depende de
/// <c>IDbConnectionFactory</c>. Si en el futuro se necesita una
/// transaccion跨-SP, se introduce aca.
/// </para>
/// </summary>
public sealed class KardexInventarioService : IKardexInventarioService
{
    private readonly IKardexEntradaRepository _entradaRepository;
    private readonly IKardexSalidaRepository _salidaRepository;
    private readonly IKardexStockRepository _stockRepository;
    private readonly KardexInventarioValidator _validator;
    private readonly IExcelExporter _exporter;
    private readonly ILogger<KardexInventarioService> _logger;

    public KardexInventarioService(
        IKardexEntradaRepository entradaRepository,
        IKardexSalidaRepository salidaRepository,
        IKardexStockRepository stockRepository,
        KardexInventarioValidator validator,
        IExcelExporter exporter,
        ILogger<KardexInventarioService> logger)
    {
        _entradaRepository = entradaRepository ?? throw new ArgumentNullException(nameof(entradaRepository));
        _salidaRepository = salidaRepository ?? throw new ArgumentNullException(nameof(salidaRepository));
        _stockRepository = stockRepository ?? throw new ArgumentNullException(nameof(stockRepository));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ============================================================================
    // KardexEntrada
    // ============================================================================

    public async Task<IReadOnlyList<KardexEntradaResponseDto>> ListarEntradasAsync(
        KardexFiltroInventarioDto filtro,
        CancellationToken ct = default)
    {
        filtro ??= new KardexFiltroInventarioDto();

        _logger.LogDebug(
            "Listando entradas de Kardex. Filtro: idEspecialidad={IdEspecialidad} idProyecto={IdProyecto} idProveedor={IdProveedor} fechaDesde={FechaDesde} fechaHasta={FechaHasta}",
            filtro.IdEspecialidad, filtro.IdProyecto, filtro.IdProveedor, filtro.FechaDesde, filtro.FechaHasta);

        var resultado = await EjecutarAsync(
            () => _entradaRepository.ListarAsync(filtro, ct),
            "ListarEntradas");
        return resultado;
    }

    public async Task<KardexEntradaResponseDto> RegistrarEntradaAsync(
        KardexEntradaCreateDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // El validator ya lanza DatosInvalidosValidacionException si falla.
        await _validator.ValidarEntradaAsync(dto, ct);

        _logger.LogDebug(
            "Registrando entrada de Kardex. idEspecialidad={IdEspecialidad} idMaterial={IdMaterial} cantidad={Cantidad}",
            dto.IdEspecialidad, dto.IdMaterial, dto.Cantidad);

        return await EjecutarAsync(
            () => _entradaRepository.RegistrarAsync(dto, ct),
            "RegistrarEntrada");
    }

    public async Task<KardexEntradaResponseDto> ActualizarEntradaAsync(
        KardexEntradaCreateDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.IdKardexEntrada is null or <= 0)
        {
            throw new ValidacionNegocioInventarioException(
                "El idKardexEntrada es obligatorio para actualizar.",
                new DetalleErrorValidacion(
                    Fila: null,
                    Campo: "idKardexEntrada",
                    CodigoError: CodigosErrorInventario.Validacion.CampoRequerido,
                    Mensaje: "El campo idKardexEntrada es obligatorio y debe ser mayor a 0."));
        }

        await _validator.ValidarEntradaAsync(dto, ct);

        _logger.LogDebug(
            "Actualizando entrada de Kardex. idKardexEntrada={IdKardexEntrada} cantidad={Cantidad}",
            dto.IdKardexEntrada, dto.Cantidad);

        return await EjecutarAsync(
            () => _entradaRepository.ActualizarAsync(dto, ct),
            "ActualizarEntrada");
    }

    public async Task EliminarEntradaAsync(int idKardexEntrada, CancellationToken ct = default)
    {
        if (idKardexEntrada <= 0)
        {
            throw new ValidacionNegocioInventarioException(
                "El idKardexEntrada es invalido.",
                new DetalleErrorValidacion(
                    Fila: null,
                    Campo: "idKardexEntrada",
                    CodigoError: CodigosErrorInventario.Validacion.CampoRequerido,
                    Mensaje: "El idKardexEntrada debe ser mayor a 0."));
        }

        _logger.LogDebug("Eliminando entrada de Kardex. idKardexEntrada={IdKardexEntrada}", idKardexEntrada);

        await EjecutarAsync<object?>(
            async () =>
            {
                await _entradaRepository.EliminarAsync(idKardexEntrada, ct);
                return null;
            },
            "EliminarEntrada");
    }

    // ============================================================================
    // KardexSalida
    // ============================================================================

    public async Task<IReadOnlyList<KardexSalidaResponseDto>> ListarSalidasAsync(
        KardexFiltroInventarioDto filtro,
        CancellationToken ct = default)
    {
        filtro ??= new KardexFiltroInventarioDto();

        _logger.LogDebug(
            "Listando salidas de Kardex. Filtro: idEspecialidad={IdEspecialidad} idProyecto={IdProyecto} fechaDesde={FechaDesde} fechaHasta={FechaHasta}",
            filtro.IdEspecialidad, filtro.IdProyecto, filtro.FechaDesde, filtro.FechaHasta);

        var resultado = await EjecutarAsync(
            () => _salidaRepository.ListarAsync(filtro, ct),
            "ListarSalidas");
        return resultado;
    }

    public async Task<IReadOnlyList<KardexSalidaResponseDto>> RegistrarSalidaAsync(
        KardexSalidaCreateDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await _validator.ValidarSalidaAsync(dto, ct);

        _logger.LogDebug(
            "Registrando salida de Kardex. idEspecialidad={IdEspecialidad} items={CantidadItems}",
            dto.IdEspecialidad, dto.Items?.Count ?? 0);

        return await EjecutarAsync(
            () => _salidaRepository.RegistrarAsync(dto, ct),
            "RegistrarSalida");
    }

    public async Task<IReadOnlyList<KardexSalidaResponseDto>> ActualizarSalidaAsync(
        KardexSalidaCreateDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.IdKardexSalida is null or <= 0)
        {
            throw new ValidacionNegocioInventarioException(
                "El idKardexSalida es obligatorio para actualizar.",
                new DetalleErrorValidacion(
                    Fila: null,
                    Campo: "idKardexSalida",
                    CodigoError: CodigosErrorInventario.Validacion.CampoRequerido,
                    Mensaje: "El campo idKardexSalida es obligatorio y debe ser mayor a 0."));
        }

        await _validator.ValidarSalidaAsync(dto, ct);

        _logger.LogDebug(
            "Actualizando salida de Kardex. idKardexSalida={IdKardexSalida} items={CantidadItems}",
            dto.IdKardexSalida, dto.Items?.Count ?? 0);

        return await EjecutarAsync(
            () => _salidaRepository.ActualizarAsync(dto, ct),
            "ActualizarSalida");
    }

    public async Task EliminarSalidaAsync(int idKardexSalida, CancellationToken ct = default)
    {
        if (idKardexSalida <= 0)
        {
            throw new ValidacionNegocioInventarioException(
                "El idKardexSalida es invalido.",
                new DetalleErrorValidacion(
                    Fila: null,
                    Campo: "idKardexSalida",
                    CodigoError: CodigosErrorInventario.Validacion.CampoRequerido,
                    Mensaje: "El idKardexSalida debe ser mayor a 0."));
        }

        await _validator.ValidarSalidaExisteAsync(idKardexSalida, ct);

        _logger.LogDebug("Eliminando salida de Kardex. idKardexSalida={IdKardexSalida}", idKardexSalida);

        await EjecutarAsync<object?>(
            async () =>
            {
                await _salidaRepository.EliminarAsync(idKardexSalida, ct);
                return null;
            },
            "EliminarSalida");
    }

    // ============================================================================
    // KardexStock (stock-actual)
    // ============================================================================

    public async Task<IReadOnlyList<KardexStockActualResponseDto>> ListarStockActualAsync(
        KardexStockFiltroInventarioDto filtro,
        CancellationToken ct = default)
    {
        filtro ??= new KardexStockFiltroInventarioDto();

        _logger.LogDebug(
            "Listando stock actual de Kardex. idEspecialidad={IdEspecialidad} idProyecto={IdProyecto} fechaDesde={FechaDesde} fechaHasta={FechaHasta}",
            filtro.IdEspecialidad, filtro.IdProyecto, filtro.FechaDesde, filtro.FechaHasta);

        var resultado = await EjecutarAsync(
            () => _stockRepository.ListarAsync(filtro, ct),
            "ListarStockActual");
        return resultado;
    }

    public async Task<byte[]> ExportarStockActualAsync(
        KardexStockFiltroInventarioDto filtro,
        CancellationToken ct = default)
    {
        filtro ??= new KardexStockFiltroInventarioDto();

        _logger.LogDebug(
            "Exportando stock actual de Kardex a Excel. idEspecialidad={IdEspecialidad} idProyecto={IdProyecto} fechaDesde={FechaDesde} fechaHasta={FechaHasta}",
            filtro.IdEspecialidad, filtro.IdProyecto, filtro.FechaDesde, filtro.FechaHasta);

        // 1. Obtener datos via el mismo metodo que GET /stock-actual. Asi el
        //    reporte refleja exactamente la misma vista que el JSON.
        var stocks = await EjecutarAsync(
            () => _stockRepository.ListarAsync(filtro, ct),
            "ExportarStockActual");

        // 2. Mapear al DTO de export. El contador Numero se asigna aqui (no
        //    en el DTO) para que el helper de export permanezca generico.
        var rows = stocks
            .Select((s, i) => new KardexStockExcelRow
            {
                Numero = i + 1,
                Proyecto = s.Proyecto,
                Especialidad = s.Especialidad,
                CodigoMaterial = s.CodigoMaterial,
                Nombre = s.Nombre,
                UnidadMedida = s.UnidadMedida,
                Entrada = s.TotalEntrada,
                Salida = s.TotalSalida,
                Stock = s.Stock,
                Fecha = s.FechaUltimaMovimiento
            })
            .ToList();

        // 3. Configurar la hoja. El subtitulo de filtros se construye a
        //    partir del DTO de filtro; si todos los campos son null se
        //    muestra "(sin filtros)".
        var config = new ExcelSheetConfig
        {
            SheetName = "Kardex Stock",
            Title = "CONSOLIDADO DE INVENTARIO",
            FiltersSubtitle = BuildFiltersSubtitle(filtro),
            IncludeTotalsRow = true
        };

        // 4. Delegar al helper generico. Este metodo es sincrono (NPOI
        //    construye el workbook en memoria); el await de arriba ya
        //    libero el thread del request para la consulta a BD.
        return _exporter.ExportToXlsx(rows, config);
    }

    /// <summary>
    /// Construye el subtitulo de filtros para el reporte. Formato:
    ///   - Con filtros: "Filtros: idEspecialidad=2, idProyecto=10, fecha=2026-01-01..2026-12-31"
    ///   - Sin filtros: "Filtros: (sin filtros)"
    /// </summary>
    private static string BuildFiltersSubtitle(KardexStockFiltroInventarioDto filtro)
    {
        var parts = new List<string>(4);
        if (filtro.IdEspecialidad.HasValue)
        {
            parts.Add($"idEspecialidad={filtro.IdEspecialidad.Value}");
        }
        if (filtro.IdProyecto.HasValue)
        {
            parts.Add($"idProyecto={filtro.IdProyecto.Value}");
        }
        if (filtro.FechaDesde.HasValue || filtro.FechaHasta.HasValue)
        {
            var desde = filtro.FechaDesde?.ToString("yyyy-MM-dd") ?? "...";
            var hasta = filtro.FechaHasta?.ToString("yyyy-MM-dd") ?? "...";
            parts.Add($"fecha={desde}..{hasta}");
        }
        return parts.Count > 0
            ? "Filtros: " + string.Join(", ", parts)
            : "Filtros: (sin filtros)";
    }

    // ============================================================================
    // Helpers
    // ============================================================================

    /// <summary>
    /// Ejecuta una operacion del repositorio y traduce SqlException 51100-51199
    /// a <see cref="ValidacionNegocioInventarioException"/>.
    /// SqlException fuera de ese rango se relanza (queda como 500 via middleware).
    /// </summary>
    /// <typeparam name="TResult">Tipo del resultado de la operacion.</typeparam>
    /// <param name="operacion">Delegado que ejecuta la operacion del repositorio.</param>
    /// <param name="operacionNombre">Nombre corto para logs (ej: "RegistrarEntrada").</param>
    private async Task<TResult> EjecutarAsync<TResult>(
        Func<Task<TResult>> operacion,
        string operacionNombre)
    {
        try
        {
            return await operacion();
        }
        catch (SqlException ex) when (SqlExceptionTranslator.Traducir(ex) is { } traduccion)
        {
            // 51110 (STOCK_INSUFICIENTE) puede tener un detalle largo con
            // varios items; el translator ya lo extrajo limpio.
            _logger.LogWarning(
                "Rechazo del SP en operacion {Operacion} (SqlException {Numero} -> {CodigoError}): {Mensaje}",
                operacionNombre, traduccion.NumeroSql, traduccion.CodigoError, traduccion.Mensaje);

            // 51104 KARDEX_NO_ENCONTRADO no es un error de validacion de
            // datos sino de recurso ausente: lo traducimos a 404 en lugar
            // de 422. Para eso usamos KardexNoEncontradoException, que es
            // un tipo independiente y sera manejado por el controller o
            // un futuro middleware.
            if (traduccion.CodigoError == CodigosErrorInventario.Sp.KardexNoEncontrado)
            {
                // Determinamos si es entrada o salida segun el nombre de operacion.
                var tipoKardex = operacionNombre.Contains("Salida", StringComparison.OrdinalIgnoreCase)
                    ? "salida"
                    : "entrada";
                // El id exacto lo extraemos del mensaje (formato "idKardex*=N")
                // o podriamos recibirlo como parametro. Por simplicidad,
                // lanzamos 0; el controller ya valido la ruta.
                throw new KardexNoEncontradoException(tipoKardex, idKardex: 0);
            }

            var detalle = new DetalleErrorValidacion(
                Fila: traduccion.Fila,
                Campo: string.Empty,
                CodigoError: traduccion.CodigoError,
                Mensaje: traduccion.Mensaje);

            throw new ValidacionNegocioInventarioException(new[] { detalle });
        }
    }
}
