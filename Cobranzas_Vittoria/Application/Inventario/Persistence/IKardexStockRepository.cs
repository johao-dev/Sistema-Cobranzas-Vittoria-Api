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
/// </summary>
public interface IKardexStockRepository
{
    /// <summary>
    /// Lista el stock actual consolidado con joins a maestra.
    /// Filtros opcionales: <paramref name="idEspecialidad"/> y
    /// <paramref name="idProyecto"/>. Si <paramref name="idProyecto"/>
    /// es NULL, se incluyen tanto las filas con proyecto como las globales
    /// (IdProyecto NULL) para que el front vea el inventario completo.
    /// </summary>
    /// <param name="idEspecialidad">Filtro por especialidad (opcional).</param>
    /// <param name="idProyecto">Filtro por proyecto (opcional, NULL = incluir globales).</param>
    /// <param name="ct">Token de cancelacion.</param>
    Task<IReadOnlyList<KardexStockActualResponseDto>> ListarAsync(
        int? idEspecialidad,
        int? idProyecto,
        CancellationToken ct = default);
}
