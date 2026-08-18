using Cobranzas_Vittoria.Application.Inventario.Dtos;
using Cobranzas_Vittoria.Application.Inventario.Excepciones;
using Cobranzas_Vittoria.Application.Inventario.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cobranzas_Vittoria.Controllers;

/// <summary>
/// Controller del modulo Inventario (Kardex manual) - feature aditiva.
/// Coexiste con el <see cref="KardexController"/> legacy sin reemplazarlo.
///
/// <para>
/// <b>Endpoints expuestos</b> (todos bajo <c>api/almacen/kardex</c>):
/// <list type="bullet">
///   <item><c>GET    /entradas</c>      -> listar entradas manuales (filtros: idEspecialidad, idProyecto, idProveedor, fechaDesde, fechaHasta).</item>
///   <item><c>POST   /entradas</c>      -> registrar entrada manual.</item>
///   <item><c>PUT    /entradas/{id}</c> -> actualizar entrada manual (idRuta == dto.idKardexEntrada).</item>
///   <item><c>DELETE /entradas/{id}</c> -> eliminar entrada manual (lanza 51111 si la resta deja stock negativo).</item>
///   <item><c>GET    /salidas</c>       -> listar salidas manuales (filtros: idEspecialidad, idProyecto, fechaDesde, fechaHasta).</item>
///   <item><c>POST   /salidas</c>       -> registrar salida manual (1..N items).</item>
///   <item><c>PUT    /salidas/{id}</c>  -> actualizar salida manual (reemplaza cabecera + items).</item>
///   <item><c>DELETE /salidas/{id}</c>  -> eliminar salida manual (repone stock).</item>
///   <item><c>GET    /stock-actual</c>  -> stock consolidado (filtros: idEspecialidad, idProyecto, fechaDesde, fechaHasta).</item>
///   <item><c>GET    /stock-actual/exportar-excel</c> -> descarga el stock consolidado en un archivo <c>.xlsx</c>.</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Por que este controller es NUEVO y no extiende al legacy</b>:
/// el <see cref="KardexController"/> legacy mezcla 2 endpoints con contratos
/// heterogeneos (uno devuelve un dynamic via KardexRepository, otro usa un
/// KardexSalidaCreateDto distinto). Crear un controller dedicado para
/// Inventario permite trabajar con DTOs tipados de
/// <c>Application/Inventario/Dtos/</c> y mantener el legacy intacto hasta
/// su marcado con <c>[Obsolete]</c> en la Fase 6.
/// </para>
///
/// <para>
/// <b>Manejo de errores</b>:
/// este controller delega TODO el manejo de errores al
/// <c>ApiExceptionMiddleware</c>, incluyendo:
/// <list type="bullet">
///   <item><see cref="IdRutaInconsistenteException"/>      -> 400 Bad Request.</item>
///   <item><see cref="KardexNoEncontradoException"/>        -> 404 Not Found.</item>
///   <item><c>ValidacionNegocioInventarioException</c>      -> 422 Unprocessable Entity (con lista de errores).</item>
///   <item><c>SqlException</c> fuera del rango 51100-51199 -> 500 Internal Server Error.</item>
/// </list>
/// El controller queda declarativo: solo traduce los parametros de
/// ruta / query string a DTOs y delega al service. Esto mantiene el
/// contrato HTTP uniforme con el resto de la API.
/// </para>
///
/// <para>
/// <b>Binding de <see cref="DateOnly"/> en query string</b>:
/// .NET 8 soporta <c>DateOnly</c> nativamente como tipo de model binding.
/// El cliente debe enviar la fecha en formato ISO <c>yyyy-MM-dd</c>
/// (ej: <c>?fechaDesde=2026-01-15</c>). Valores invalidos devuelven 400
/// automaticamente por el binder.
/// </para>
/// </summary>
[ApiController]
[Route("api/almacen/kardex")]
public class KardexInventarioController : ControllerBase
{
    private readonly IKardexInventarioService _service;

    public KardexInventarioController(IKardexInventarioService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
    }

    // ============================================================================
    // KardexEntrada
    // ============================================================================

    /// <summary>
    /// Lista las entradas manuales de Kardex con filtros opcionales.
    /// </summary>
    [HttpGet("entradas")]
    public async Task<IActionResult> ListarEntradas(
        [FromQuery] int? idEspecialidad,
        [FromQuery] int? idProyecto,
        [FromQuery] int? idProveedor,
        [FromQuery] DateOnly? fechaDesde,
        [FromQuery] DateOnly? fechaHasta,
        CancellationToken ct)
    {
        var filtro = new KardexFiltroInventarioDto
        {
            IdEspecialidad = idEspecialidad,
            IdProyecto = idProyecto,
            IdProveedor = idProveedor,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        };
        var data = await _service.ListarEntradasAsync(filtro, ct);
        return Ok(data);
    }

    /// <summary>
    /// Registra una nueva entrada manual de Kardex.
    /// </summary>
    [HttpPost("entradas")]
    public async Task<IActionResult> RegistrarEntrada(
        [FromBody] KardexEntradaCreateDto dto,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var creada = await _service.RegistrarEntradaAsync(dto, ct);
        return Ok(creada);
    }

    /// <summary>
    /// Actualiza una entrada manual existente. El id de la ruta debe
    /// coincidir con <c>dto.IdKardexEntrada</c>; si no, devuelve 400.
    /// </summary>
    [HttpPut("entradas/{id:int}")]
    public async Task<IActionResult> ActualizarEntrada(
        [FromRoute] int id,
        [FromBody] KardexEntradaCreateDto dto,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidarIdRuta(id, dto.IdKardexEntrada, "idKardexEntrada");
        var actualizada = await _service.ActualizarEntradaAsync(dto, ct);
        return Ok(actualizada);
    }

    /// <summary>
    /// Elimina una entrada manual. Si la resta deja stock negativo,
    /// el SP responde 51111 y el service la traduce a 422.
    /// </summary>
    [HttpDelete("entradas/{id:int}")]
    public async Task<IActionResult> EliminarEntrada(
        [FromRoute] int id,
        CancellationToken ct)
    {
        await _service.EliminarEntradaAsync(id, ct);
        return Ok(new { ok = true });
    }

    // ============================================================================
    // KardexSalida
    // ============================================================================

    /// <summary>
    /// Lista las salidas manuales de Kardex con sus items.
    /// El SP devuelve una fila por item repitiendo la cabecera.
    /// </summary>
    [HttpGet("salidas")]
    public async Task<IActionResult> ListarSalidas(
        [FromQuery] int? idEspecialidad,
        [FromQuery] int? idProyecto,
        [FromQuery] DateOnly? fechaDesde,
        [FromQuery] DateOnly? fechaHasta,
        CancellationToken ct)
    {
        var filtro = new KardexFiltroInventarioDto
        {
            IdEspecialidad = idEspecialidad,
            IdProyecto = idProyecto,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        };
        var data = await _service.ListarSalidasAsync(filtro, ct);
        return Ok(data);
    }

    /// <summary>
    /// Registra una salida manual de Kardex con 1..N items.
    /// </summary>
    [HttpPost("salidas")]
    public async Task<IActionResult> RegistrarSalida(
        [FromBody] KardexSalidaCreateDto dto,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var creada = await _service.RegistrarSalidaAsync(dto, ct);
        return Ok(creada);
    }

    /// <summary>
    /// Actualiza una salida manual existente (reemplaza cabecera + items).
    /// El id de la ruta debe coincidir con <c>dto.IdKardexSalida</c>.
    /// </summary>
    [HttpPut("salidas/{id:int}")]
    public async Task<IActionResult> ActualizarSalida(
        [FromRoute] int id,
        [FromBody] KardexSalidaCreateDto dto,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidarIdRuta(id, dto.IdKardexSalida, "idKardexSalida");
        var actualizada = await _service.ActualizarSalidaAsync(dto, ct);
        return Ok(actualizada);
    }

    /// <summary>
    /// Elimina una salida manual. El SP hace CASCADE de los detalles
    /// y repone el stock automaticamente.
    /// </summary>
    [HttpDelete("salidas/{id:int}")]
    public async Task<IActionResult> EliminarSalida(
        [FromRoute] int id,
        CancellationToken ct)
    {
        await _service.EliminarSalidaAsync(id, ct);
        return Ok(new { ok = true });
    }

    // ============================================================================
    // KardexStock (stock-actual)
    // ============================================================================

    /// <summary>
    /// Lista el stock actual consolidado por (material, especialidad, proyecto).
    /// Filtros: idEspecialidad, idProyecto, fechaDesde, fechaHasta
    /// (filtran sobre <c>FechaUltimaMovimiento</c>, no sobre la fecha del movimiento).
    /// </summary>
    [HttpGet("stock-actual")]
    public async Task<IActionResult> ListarStockActual(
        [FromQuery] int? idEspecialidad,
        [FromQuery] int? idProyecto,
        [FromQuery] DateOnly? fechaDesde,
        [FromQuery] DateOnly? fechaHasta,
        CancellationToken ct)
    {
        var filtro = new KardexStockFiltroInventarioDto
        {
            IdEspecialidad = idEspecialidad,
            IdProyecto = idProyecto,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        };
        var data = await _service.ListarStockActualAsync(filtro, ct);
        return Ok(data);
    }

    /// <summary>
    /// Exporta el stock actual consolidado a un archivo <c>.xlsx</c> listo
    /// para descargar. Mismos filtros que <c>GET /stock-actual</c>.
    ///
    /// <para>
    /// <b>Respuesta</b>: <c>application/vnd.openxmlformats-officedocument.spreadsheetml.sheet</c>
    /// con header <c>Content-Disposition: attachment; filename="kardex-stock-{yyyyMMdd-HHmm}.xlsx"</c>.
    /// El cuerpo contiene: titulo, subtitulo de filtros, fecha de generacion,
    /// header de columnas, filas de datos y fila de totales (suma de Entrada/Salida/Stock).
    /// </para>
    ///
    /// <para>
    /// <b>Por que un endpoint separado en lugar de <c>?format=xlsx</c> en
    /// el GET JSON</b>: la negociacion de contenido para un binario es
    /// engorrosa en .NET; un endpoint dedicado es mas simple para el
    /// cliente (solo cambia la URL y dispara la descarga) y mantiene
    /// <c>GET /stock-actual</c> 100% JSON.
    /// </para>
    /// </summary>
    [HttpGet("stock-actual/exportar-excel")]
    public async Task<IActionResult> ExportarStockActual(
        [FromQuery] int? idEspecialidad,
        [FromQuery] int? idProyecto,
        [FromQuery] DateOnly? fechaDesde,
        [FromQuery] DateOnly? fechaHasta,
        CancellationToken ct)
    {
        var filtro = new KardexStockFiltroInventarioDto
        {
            IdEspecialidad = idEspecialidad,
            IdProyecto = idProyecto,
            FechaDesde = fechaDesde,
            FechaHasta = fechaHasta
        };

        var bytes = await _service.ExportarStockActualAsync(filtro, ct);

        // Nombre del archivo: kardex-stock-{yyyyMMdd-HHmm}.xlsx. Esto permite
        // al usuario descargar multiples reportes en la misma sesion sin
        // pisar archivos locales.
        var fileName = $"kardex-stock-{DateTime.Now:yyyyMMdd-HHmm}.xlsx";

        return File(
            fileContents: bytes,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileDownloadName: fileName);
    }

    // ============================================================================
    // Helpers privados
    // ============================================================================

    /// <summary>
    /// Valida que el id de la ruta coincida con el id del cuerpo del DTO.
    /// Lanza <see cref="IdRutaInconsistenteException"/> si no coinciden.
    /// La excepcion burbujea hasta el <c>ApiExceptionMiddleware</c>, que
    /// la traduce a HTTP 400 con codigo <c>ID_RUTA_INCONSISTENTE</c>.
    /// </summary>
    private static void ValidarIdRuta(int idRuta, int? idCuerpo, string campoCuerpo)
    {
        if (idCuerpo is null || idCuerpo.Value != idRuta)
        {
            throw new IdRutaInconsistenteException(idRuta, idCuerpo, campoCuerpo);
        }
    }
}
