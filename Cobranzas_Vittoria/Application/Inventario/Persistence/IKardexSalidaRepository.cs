using Cobranzas_Vittoria.Application.Inventario.Dtos;

namespace Cobranzas_Vittoria.Application.Inventario.Persistence;

/// <summary>
/// Contrato de persistencia para KardexSalida (salidas manuales con 1..N items).
///
/// <para>
/// <b>Patron TVP</b>: usa el TVP <c>almacen.TVP_KardexSalidaItem</c> y reutiliza
/// el <c>TvpMapper</c> de <c>Application/Importacion/Persistence</c> para
/// convertir la lista de items a DataTable. La conversion DTO -&gt; TVP la
/// hace el repositorio internamente, de modo que el service no conoce
/// <c>System.Data.DataTable</c>.
/// </para>
///
/// <para>
/// <b>Una fila por item</b>: el SP lista repitiendo cabecera por cada item
/// (mismo patron que KardexResumenMaterial legacy). El repositorio devuelve
/// <c>IReadOnlyList&lt;KardexSalidaResponseDto&gt;</c> con N entradas por
/// cada KardexSalida, una por cada item.
/// </para>
///
/// <para>
/// <b>Por que no es generico sobre el TVP</b>:
/// a diferencia de Importacion (donde el mismo patron aplica a 7 modulos),
/// KardexSalida es un caso unico. Parametrizar el nombre del TVP solo
/// agregaria confusion sin reducir duplicacion.
/// </para>
/// </summary>
public interface IKardexSalidaRepository
{
    /// <summary>
    /// Lista salidas con los filtros indicados. Una fila por cada item,
    /// repitiendo los datos de cabecera.
    /// </summary>
    /// <param name="filtro">Filtro de busqueda (todos los campos son opcionales).</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<IReadOnlyList<KardexSalidaResponseDto>> ListarAsync(
        KardexFiltroInventarioDto filtro,
        CancellationToken ct = default);

    /// <summary>
    /// Inserta una salida con sus items. Valida stock para cada item dentro
    /// del SP (lanza 51110 si falta). Devuelve la salida con todos sus items
    /// ya persistidos.
    /// </summary>
    /// <param name="dto">Datos de la salida a crear. <c>Items</c> debe tener al menos un elemento.</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<IReadOnlyList<KardexSalidaResponseDto>> RegistrarAsync(
        KardexSalidaCreateDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Actualiza una salida existente: reemplaza cabecera + items en TX.
    /// Calcula el diff por triada antes de aplicarlo a KardexStock.
    /// </summary>
    /// <param name="dto">Datos actualizados. <c>IdKardexSalida</c> es obligatorio.</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<IReadOnlyList<KardexSalidaResponseDto>> ActualizarAsync(
        KardexSalidaCreateDto dto,
        CancellationToken ct = default);

    /// <summary>
    /// Elimina una salida. CASCADE borra sus detalles. El SP repone el stock
    /// agrupando por triada (IdMaterial, IdEspecialidad, IdProyecto).
    /// </summary>
    /// <param name="idKardexSalida">PK de la salida a eliminar.</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task EliminarAsync(
        int idKardexSalida,
        CancellationToken ct = default);
}
