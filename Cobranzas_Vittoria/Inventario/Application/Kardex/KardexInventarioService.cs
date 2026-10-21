using Cobranzas_Vittoria.Application.Common.Excepciones;
using Cobranzas_Vittoria.Application.Common.Exports;
using Cobranzas_Vittoria.Inventario.Domain.Model;
using Cobranzas_Vittoria.Inventario.Application.Excepciones;
using Cobranzas_Vittoria.Inventario.Application.Kardex.ExportarStock;
using Cobranzas_Vittoria.Inventario.Domain.Persistence;
using Cobranzas_Vittoria.Inventario.Application.Common;

namespace Cobranzas_Vittoria.Inventario.Application.Kardex;

/// <summary>
/// Coordina las validaciones, la persistencia y la exportación del Kardex manual.
/// Los repositorios traducen los rechazos SQL antes de devolverlos a la aplicación.
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

    public async Task<IReadOnlyList<KardexEntrada>> ListarEntradasAsync(
        FiltroKardex filtro,
        CancellationToken ct = default)
    {
        filtro ??= new FiltroKardex();

        _logger.LogDebug(
            "Listando entradas de Kardex. Filtro: idEspecialidad={IdEspecialidad} idProyecto={IdProyecto} idProveedor={IdProveedor} fechaDesde={FechaDesde} fechaHasta={FechaHasta}",
            filtro.IdEspecialidad, filtro.IdProyecto, filtro.IdProveedor, filtro.FechaDesde, filtro.FechaHasta);

        IReadOnlyList<KardexEntrada> resultado = await _entradaRepository.ListarAsync(filtro, ct);
        return resultado;
    }

    public async Task<KardexEntrada> RegistrarEntradaAsync(
        RegistroKardexEntrada dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        // El validator ya lanza DatosInvalidosValidacionException si falla.
        await _validator.ValidarEntradaAsync(dto, ct);

        _logger.LogDebug(
            "Registrando entrada de Kardex. idMaterial={IdMaterial} cantidad={Cantidad}",
            dto.IdMaterial, dto.Cantidad);

        return await _entradaRepository.RegistrarAsync(dto, ct);
    }

    public async Task<KardexEntrada> ActualizarEntradaAsync(
        RegistroKardexEntrada dto,
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

        return await _entradaRepository.ActualizarAsync(dto, ct);
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

        await _entradaRepository.EliminarAsync(idKardexEntrada, ct);
    }

    // ============================================================================
    // KardexSalida
    // ============================================================================

    public async Task<IReadOnlyList<KardexSalida>> ListarSalidasAsync(
        FiltroKardex filtro,
        CancellationToken ct = default)
    {
        filtro ??= new FiltroKardex();

        _logger.LogDebug(
            "Listando salidas de Kardex. Filtro: idEspecialidad={IdEspecialidad} idProyecto={IdProyecto} fechaDesde={FechaDesde} fechaHasta={FechaHasta}",
            filtro.IdEspecialidad, filtro.IdProyecto, filtro.FechaDesde, filtro.FechaHasta);

        IReadOnlyList<KardexSalida> resultado = await _salidaRepository.ListarAsync(filtro, ct);
        return resultado;
    }

    public async Task<IReadOnlyList<KardexSalida>> RegistrarSalidaAsync(
        RegistroKardexSalida dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        await _validator.ValidarSalidaAsync(dto, ct);

        _logger.LogDebug(
            "Registrando salida de Kardex. items={CantidadItems}",
            dto.Items?.Count ?? 0);

        return await _salidaRepository.RegistrarAsync(dto, ct);
    }

    public async Task<IReadOnlyList<KardexSalida>> ActualizarSalidaAsync(
        RegistroKardexSalida dto,
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

        return await _salidaRepository.ActualizarAsync(dto, ct);
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

        await _salidaRepository.EliminarAsync(idKardexSalida, ct);
    }

    // ============================================================================
    // KardexStock (stock-actual)
    // ============================================================================

    public async Task<IReadOnlyList<StockKardex>> ListarStockActualAsync(
        FiltroStockKardex filtro,
        CancellationToken ct = default)
    {
        filtro ??= new FiltroStockKardex();

        _logger.LogDebug(
            "Listando stock actual de Kardex. idEspecialidad={IdEspecialidad} fechaDesde={FechaDesde} fechaHasta={FechaHasta}",
            filtro.IdEspecialidad, filtro.FechaDesde, filtro.FechaHasta);

        IReadOnlyList<StockKardex> resultado = await _stockRepository.ListarAsync(filtro, ct);
        return resultado;
    }

    public async Task<byte[]> ExportarStockActualAsync(
        FiltroStockKardex filtro,
        bool? incluirTotales = null,
        CancellationToken ct = default)
    {
        filtro ??= new FiltroStockKardex();

        _logger.LogDebug(
            "Exportando stock actual de Kardex a Excel. idEspecialidad={IdEspecialidad} fechaDesde={FechaDesde} fechaHasta={FechaHasta}",
            filtro.IdEspecialidad, filtro.FechaDesde, filtro.FechaHasta);

        // 1. Obtener datos via el mismo metodo que GET /stock-actual. Asi el
        //    reporte refleja exactamente la misma vista que el JSON.
        IReadOnlyList<StockKardex> stocks = await _stockRepository.ListarAsync(filtro, ct);

        // 2. Mapear al DTO de export. El contador Numero se asigna aqui (no
        //    en el DTO) para que el helper de export permanezca generico.
        List<KardexStockExcelRow> rows = stocks
            .Select((s, i) => new KardexStockExcelRow
            {
                Numero = i + 1,
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
        ExcelSheetConfig config = new ExcelSheetConfig
        {
            SheetName = "Kardex Stock",
            Title = "CONSOLIDADO DE INVENTARIO",
            FiltersSubtitle = BuildFiltersSubtitle(filtro),
            IncludeTotalsRow = incluirTotales ?? false
        };

        // 4. Delegar al helper generico. Este metodo es sincrono (NPOI
        //    construye el workbook en memoria); el await de arriba ya
        //    libero el thread del request para la consulta a BD.
        return _exporter.ExportToXlsx(rows, config);
    }

    /// <summary>
    /// Construye el subtitulo de filtros para el reporte. Formato:
    ///   - Con filtros: "Filtros: idEspecialidad=2, fecha=2026-01-01..2026-12-31"
    ///   - Sin filtros: "Filtros: (sin filtros)"
    /// </summary>
    private static string BuildFiltersSubtitle(FiltroStockKardex filtro)
    {
        List<string> parts = new List<string>(4);
        if (filtro.IdEspecialidad.HasValue)
        {
            parts.Add($"idEspecialidad={filtro.IdEspecialidad.Value}");
        }
        if (filtro.FechaDesde.HasValue || filtro.FechaHasta.HasValue)
        {
            string desde = filtro.FechaDesde?.ToString("yyyy-MM-dd") ?? "...";
            string hasta = filtro.FechaHasta?.ToString("yyyy-MM-dd") ?? "...";
            parts.Add($"fecha={desde}..{hasta}");
        }
        return parts.Count > 0
            ? "Filtros: " + string.Join(", ", parts)
            : "Filtros: (sin filtros)";
    }

}
