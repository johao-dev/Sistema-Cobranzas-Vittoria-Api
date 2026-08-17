namespace Cobranzas_Vittoria.Application.Inventario.Dtos;

/// <summary>
/// Filtro de busqueda para el listado consolidado de stock actual
/// (<c>GET /api/almacen/kardex/stock-actual</c>).
///
/// <para>
/// <b>Por que existe como DTO aparte (no reutiliza <c>KardexFiltroInventarioDto</c>)</b>:
/// el stock-actual NO filtra por proveedor (no tiene sentido: el stock
/// es por material, no por proveedor). Ademas, los filtros de fecha en
/// stock-actual actuan sobre <c>FechaUltimaMovimiento</c> (no sobre
/// <c>Fecha</c> del movimiento), asi que la semantica diverge.
/// Mantener DTOs separados evita acoplar dos endpoints con requisitos
/// sutilmente distintos.
/// </para>
///
/// <para>
/// <b>Convención de fechas</b>: se reciben como <see cref="DateOnly"/>
/// (no <see cref="DateTime"/>) para que el binding del controller no tenga
/// que convertir. El SP recibe el tipo SQL <c>date</c> y filtra
/// <c>FechaUltimaMovimiento &gt;= @FechaDesde AND &lt;= @FechaHasta</c>.
/// </para>
///
/// <para>
/// <b>Por que es un record</b>:
/// es un DTO de query string sin identidad ni comportamiento. <c>sealed record</c>
/// garantiza inmutabilidad y comparacion por valor, y permite
/// deconstructar en controllers si fuera necesario.
/// </para>
/// </summary>
public sealed record KardexStockFiltroInventarioDto
{
    /// <summary>Filtra por especialidad (ej: "Estructuras", "Instalaciones electricas").</summary>
    public int? IdEspecialidad { get; init; }

    /// <summary>
    /// Filtra por proyecto. NULL = traer tanto las filas con proyecto asignado
    /// como las globales (IdProyecto NULL en KardexStock). Esto refleja la
    /// regla de negocio "el stock global es visible en todos los proyectos".
    /// </summary>
    public int? IdProyecto { get; init; }

    /// <summary>Fecha minima de ultima actualizacion del stock (inclusive).</summary>
    public DateOnly? FechaDesde { get; init; }

    /// <summary>Fecha maxima de ultima actualizacion del stock (inclusive).</summary>
    public DateOnly? FechaHasta { get; init; }
}
