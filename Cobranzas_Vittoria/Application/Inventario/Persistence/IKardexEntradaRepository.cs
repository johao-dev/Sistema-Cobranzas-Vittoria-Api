using Cobranzas_Vittoria.Application.Inventario.Dtos;

namespace Cobranzas_Vittoria.Application.Inventario.Persistence;

/// <summary>
/// Contrato de persistencia para KardexEntrada (entradas manuales).
///
/// <para>
/// <b>Responsabilidad</b>: ejecutar los SPs de <c>almacen.usp_KardexEntrada_*</c>
/// y devolver los resultados ya mapeados a DTOs tipados. NO contiene logica
/// de negocio: las validaciones viven en <c>KardexInventarioValidator</c>
/// y la traduccion de errores del SP vive en el <c>KardexInventarioService</c>
/// (usando <c>SqlExceptionTranslator</c>).
/// </para>
///
/// <para>
/// <b>Por que no extiende <c>IImportRepository</c></b>:
/// ImportRepository esta optimizado para el patron TVP (recibe nombre del SP
/// y nombre del TVP). KardexEntrada NO usa TVP: cada operacion es un solo
/// registro con parametros escalares. Un contrato dedicado evita acoplar la
/// API a un patron que no aplica.
/// </para>
///
/// <para>
/// <b>Parametros de filtro</b>: cuando se omite un filtro, el SP lo ignora
/// (logica <c>@x IS NULL OR = @x</c>). Esto se logra pasando <c>null</c>
/// desde el repositorio, sin necesidad de un wrapper.
/// </para>
/// </summary>
public interface IKardexEntradaRepository
{
    /// <summary>
    /// Lista entradas con los filtros indicados. Ordena por Fecha DESC.
    /// </summary>
    /// <param name="filtro">Filtro de busqueda (todos los campos son opcionales).</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<IReadOnlyList<KardexEntradaResponseDto>> ListarAsync(
        KardexFiltroInventarioDto filtro,
        CancellationToken ct = default);

    /// <summary>
    /// Inserta una entrada y devuelve la fila creada con sus joins a maestra.
    /// Lanza <see cref="Microsoft.Data.SqlClient.SqlException"/> con numeros
    /// 51100-51199 si la validacion del SP falla.
    /// </summary>
    /// <param name="dto">Datos de la entrada a crear.</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<KardexEntradaResponseDto> RegistrarAsync(
        KardexEntradaCreateDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Actualiza una entrada existente. Si la triada (IdMaterial, IdEspecialidad, IdProyecto)
    /// cambia, el SP hace rollback del stock en la triada vieja y aplicacion
    /// en la nueva, todo en la misma TX. Lanza 51104 si el id no existe.
    /// </summary>
    /// <param name="dto">Datos actualizados. <c>IdKardexEntrada</c> es obligatorio.</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<KardexEntradaResponseDto> ActualizarAsync(
        KardexEntradaCreateDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina una entrada. Si la resta dejaria el stock por debajo de las
    /// salidas ya consumidas, el SP lanza 51111 (STOCK_INCONSISTENTE_AL_ELIMINAR).
    /// </summary>
    /// <param name="idKardexEntrada">PK de la entrada a eliminar.</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task EliminarAsync(
        int idKardexEntrada,
        CancellationToken ct = default);
}
