using Cobranzas_Vittoria.Application.Inventario.Dtos;

namespace Cobranzas_Vittoria.Application.Inventario.Services;

/// <summary>
/// Fachada del modulo Inventario (Kardex manual). Es el unico punto de
/// entrada que consume el controller. Su responsabilidad es:
///
///   1. Validar el DTO de entrada usando <c>KardexInventarioValidator</c>
///      (reglas de negocio que no requieren BD).
///   2. Delegar al repositorio correspondiente (entrada / salida / stock).
///   3. Traducir las <c>SqlException</c> 51100-51199 a
///      <c>ValidacionNegocioInventarioException</c> (que extiende
///      <c>DatosInvalidosException</c>) para que el middleware la mapee
///      a HTTP 422 con la lista de errores.
///
/// <para>
/// <b>No traduce excepciones de validacion</b>: el validator ya lanza
/// <c>DatosInvalidosException</c> con el formato correcto. El service
/// propaga esa excepcion sin envolverla.
/// </para>
///
/// <para>
/// <b>Por que el service es async y devuelve DTOs tipados</b>:
///   - Async para no bloquear el thread del request mientras el SP ejecuta.
///   - DTOs tipados (no <c>dynamic</c>) para que el controller compile
///     con chequeo estatico.
/// </para>
/// </summary>
public interface IKardexInventarioService
{
    // ============================================================================
    // KardexEntrada
    // ============================================================================

    /// <summary>Lista entradas manuales con filtros opcionales.</summary>
    Task<IReadOnlyList<KardexEntradaResponseDto>> ListarEntradasAsync(
        KardexFiltroInventarioDto filtro,
        CancellationToken ct = default);

    /// <summary>Registra una entrada manual.</summary>
    /// <exception cref="Cobranzas_Vittoria.Application.Importacion.Excepciones.DatosInvalidosException">
    /// Si la validacion falla (HTTP 422).</exception>
    Task<KardexEntradaResponseDto> RegistrarEntradaAsync(
        KardexEntradaCreateDto dto,
        CancellationToken ct = default);

    /// <summary>Actualiza una entrada existente.</summary>
    /// <exception cref="Cobranzas_Vittoria.Application.Importacion.Excepciones.DatosInvalidosException">
    /// Si la validacion falla o el SP rechaza (HTTP 422).</exception>
    /// <exception cref="Cobranzas_Vittoria.Application.Inventario.Excepciones.KardexNoEncontradoException">
    /// Si el id no existe (HTTP 404, no implementado aqui; lo lanza el SP y el controller lo traduce).</exception>
    Task<KardexEntradaResponseDto> ActualizarEntradaAsync(
        KardexEntradaCreateDto dto,
        CancellationToken ct = default);

    /// <summary>Elimina una entrada. Lanza 51111 si la resta deja stock negativo.</summary>
    Task EliminarEntradaAsync(
        int idKardexEntrada,
        CancellationToken ct = default);

    // ============================================================================
    // KardexSalida
    // ============================================================================

    /// <summary>Lista salidas manuales con sus items, repitiendo cabecera por cada item.</summary>
    Task<IReadOnlyList<KardexSalidaResponseDto>> ListarSalidasAsync(
        KardexFiltroInventarioDto filtro,
        CancellationToken ct = default);

    /// <summary>Registra una salida manual con 1..N items.</summary>
    /// <exception cref="Cobranzas_Vittoria.Application.Importacion.Excepciones.DatosInvalidosException">
    /// Si la validacion falla o falta stock (HTTP 422).</exception>
    Task<IReadOnlyList<KardexSalidaResponseDto>> RegistrarSalidaAsync(
        KardexSalidaCreateDto dto,
        CancellationToken ct = default);

    /// <summary>Actualiza una salida existente (reemplaza cabecera + items).</summary>
    Task<IReadOnlyList<KardexSalidaResponseDto>> ActualizarSalidaAsync(
        KardexSalidaCreateDto dto,
        CancellationToken ct = default);

    /// <summary>Elimina una salida (CASCADE borra los detalles; el stock se repone).</summary>
    Task EliminarSalidaAsync(
        int idKardexSalida,
        CancellationToken ct = default);

    // ============================================================================
    // KardexStock (stock-actual)
    // ============================================================================

    /// <summary>Lista el stock actual consolidado con filtros opcionales (especialidad, proyecto, rango de fechas).</summary>
    Task<IReadOnlyList<KardexStockActualResponseDto>> ListarStockActualAsync(
        KardexStockFiltroInventarioDto filtro,
        CancellationToken ct = default);
}
