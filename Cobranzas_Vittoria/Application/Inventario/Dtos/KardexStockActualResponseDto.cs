namespace Cobranzas_Vittoria.Application.Inventario.Dtos;

/// <summary>
/// DTO de salida que representa una fila del inventario consolidado
/// (almacen.KardexStock) con los joins a maestra para que el front reciba
/// nombres legibles. Se devuelve en GET /api/almacen/kardex/stock-actual.
///
/// <para>
/// <b>Por que expone TotalEntrada y TotalSalida ademas de Stock</b>:
/// permite al frontend mostrar "Entradas / Salidas / Stock" sin un
/// segundo round-trip. El SP ya los calcula y proyecta, asi que el
/// costo de incluirlos es cero.
/// </para>
///
/// <para>
/// <b>Por que incluye IdKardexStock</b>:
/// facilita el debug en soporte (el operador puede consultar la fila
/// exacta) aunque el front raramente lo muestre.
/// </para>
///
/// <para>
/// <b>Por que UnidadMedida viene como <c>string</c></b>:
/// el SP la proyecta como el <c>UnidadMedida</c> de maestra.Material
/// (ej: "KG", "M", "UND"). El cliente la muestra directamente sin
/// necesidad de mapear un catalogo.
/// </para>
/// </summary>
public sealed class KardexStockActualResponseDto
{
    public int IdKardexStock { get; set; }
    public int IdMaterial { get; set; }
    public string? CodigoMaterial { get; set; }
    public string? Nombre { get; set; }
    public string? UnidadMedida { get; set; }
    public int IdEspecialidad { get; set; }
    public string? Especialidad { get; set; }
    public int? IdProyecto { get; set; }
    public string? Proyecto { get; set; }
    public decimal TotalEntrada { get; set; }
    public decimal TotalSalida { get; set; }
    public decimal Stock { get; set; }
    public DateOnly FechaUltimaMovimiento { get; set; }
}
