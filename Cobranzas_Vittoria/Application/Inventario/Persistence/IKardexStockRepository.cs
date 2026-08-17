using Cobranzas_Vittoria.Application.Inventario.Dtos;

namespace Cobranzas_Vittoria.Application.Inventario.Persistence;

/// <summary>
/// Contrato de persistencia para el inventario consolidado (almacen.KardexStock).
///
/// <para>
/// <b>Por que existe como interfaz separada (no parte de KardexEntrada o KardexSalida)</b>:
/// el stock-actual es un agregado de SOLO LECTURA. No tiene operacion de
/// escritura desde el API (todas las mutaciones se hacen transitivamente
/// desde KardexEntrada / KardexSalida). Separar el contrato deja claro
/// que este repositorio no puede mutar el stock, y permite a futuro
/// optimizar la consulta (cache, vista materializada, etc) sin tocar los
/// repositorios de escritura.
/// </para>
///
/// <para>
/// <b>Por que la vista <c>vw_Kardex_StockActual_v2</c> no se usa</b>:
/// el SP <c>usp_Kardex_StockActual_Listar</c> ya inlina los JOINs a
/// maestra y ordena por NombreEspecialidad, NombreMaterial. Consumir
/// directamente el SP evita el acoplamiento a una vista que solo agrega
/// un nivel de indireccion sin valor aqui.
/// </para>
///
/// <para>
/// <b>Filtros soportados por el SP</b>: <c>IdEspecialidad</c>,
/// <c>IdProyecto</c>, <c>FechaDesde</c> y <c>FechaHasta</c>. Si
/// <c>IdProyecto</c> es NULL, se incluyen tanto las filas con proyecto
/// como las globales (IdProyecto NULL en KardexStock) para que el front
/// vea el inventario completo. Si <c>FechaDesde</c> o <c>FechaHasta</c>
/// son NULL, no se filtra por ese extremo (rango abierto).
/// </para>
/// </summary>
public interface IKardexStockRepository
{
    /// <summary>
    /// Lista el stock actual consolidado con joins a maestra.
    /// </summary>
    /// <param name="filtro">Filtro de busqueda (todos los campos son opcionales).</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<IReadOnlyList<KardexStockActualResponseDto>> ListarAsync(
        KardexStockFiltroInventarioDto filtro,
        CancellationToken ct = default);
}
