using Cobranzas_Vittoria.Inventario.Domain.Model;

namespace Cobranzas_Vittoria.Inventario.Application.Kardex;

/// <summary>
/// Fachada de los casos de uso de Kardex manual: valida las operaciones,
/// delega la persistencia y genera la exportación del stock consolidado.
/// </summary>
public interface IKardexInventarioService
{
    // ============================================================================
    // KardexEntrada
    // ============================================================================

    /// <summary>Lista entradas manuales con filtros opcionales.</summary>
    Task<IReadOnlyList<KardexEntrada>> ListarEntradasAsync(
        FiltroKardex filtro,
        CancellationToken ct = default);

    /// <summary>Registra una entrada manual.</summary>
    /// <exception cref="Cobranzas_Vittoria.Inventario.Application.Excepciones.ValidacionNegocioInventarioException">
    /// Si la validacion falla (HTTP 422).</exception>
    Task<KardexEntrada> RegistrarEntradaAsync(
        RegistroKardexEntrada dto,
        CancellationToken ct = default);

    /// <summary>Actualiza una entrada existente.</summary>
    /// <exception cref="Cobranzas_Vittoria.Inventario.Application.Excepciones.ValidacionNegocioInventarioException">
    /// Si la validacion falla o el SP rechaza (HTTP 422).</exception>
    /// <exception cref="Cobranzas_Vittoria.Inventario.Domain.Excepciones.KardexNoEncontradoException">
    /// Si el id no existe; el middleware la traduce a HTTP 404.</exception>
    Task<KardexEntrada> ActualizarEntradaAsync(
        RegistroKardexEntrada dto,
        CancellationToken ct = default);

    /// <summary>Elimina una entrada. Lanza 51111 si la resta deja stock negativo.</summary>
    Task EliminarEntradaAsync(
        int idKardexEntrada,
        CancellationToken ct = default);

    // ============================================================================
    // KardexSalida
    // ============================================================================

    /// <summary>Lista salidas manuales con sus items, repitiendo cabecera por cada item.</summary>
    Task<IReadOnlyList<KardexSalida>> ListarSalidasAsync(
        FiltroKardex filtro,
        CancellationToken ct = default);

    /// <summary>Registra una salida manual con 1..N items.</summary>
    /// <exception cref="Cobranzas_Vittoria.Inventario.Application.Excepciones.ValidacionNegocioInventarioException">
    /// Si la validacion falla o falta stock (HTTP 422).</exception>
    Task<IReadOnlyList<KardexSalida>> RegistrarSalidaAsync(
        RegistroKardexSalida dto,
        CancellationToken ct = default);

    /// <summary>Actualiza una salida existente (reemplaza cabecera + items).</summary>
    Task<IReadOnlyList<KardexSalida>> ActualizarSalidaAsync(
        RegistroKardexSalida dto,
        CancellationToken ct = default);

    /// <summary>Elimina una salida (CASCADE borra los detalles; el stock se repone).</summary>
    Task EliminarSalidaAsync(
        int idKardexSalida,
        CancellationToken ct = default);

    // ============================================================================
    // KardexStock (stock-actual)
    // ============================================================================

    /// <summary>Lista el stock actual consolidado con filtros opcionales (especialidad y rango de fechas).</summary>
    Task<IReadOnlyList<StockKardex>> ListarStockActualAsync(
        FiltroStockKardex filtro,
        CancellationToken ct = default);

    /// <summary>
    /// Exporta el stock actual consolidado a un archivo <c>.xlsx</c> (bytes)
    /// listo para ser devuelto por el controller como <c>FileContentResult</c>.
    /// Los filtros aplicados se incluyen como subtitulo de la hoja.
    /// </summary>
    /// <param name="filtro">
    /// Filtros opcionales. Si es null se trata como filtro vacio (lista completa).
    /// </param>
    /// <param name="incluirTotales">Indica si se agrega una fila de totales.</param>
    /// <param name="ct">Token de cancelacion.</param>
    /// <returns>
    /// Bytes del archivo <c>.xlsx</c> con:
    /// titulo, subtitulo de filtros, fecha de generacion, header de columnas,
    /// filas de datos y fila de totales (suma de Entrada/Salida/Stock).
    /// </returns>
    Task<byte[]> ExportarStockActualAsync(
        FiltroStockKardex filtro,
        bool? incluirTotales,
        CancellationToken ct = default);
}
