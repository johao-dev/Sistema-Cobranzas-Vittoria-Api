namespace Cobranzas_Vittoria.Application.Inventario.Dtos;

/// <summary>
/// Filtro de busqueda para listar entradas o salidas de Kardex manual.
/// Todos los campos son opcionales: si se omite un campo, el SP no aplica
/// ese filtro (comportamiento "IS NULL OR = @param" tipico de los SPs del modulo).
///
/// <para>
/// <b>Por que es un record y no una clase mutable</b>:
/// es un DTO de entrada (query string), no tiene comportamiento ni identidad
/// propia. Un <c>sealed record</c> garantiza inmutabilidad, igualdad por
/// valor y permite deconstructar si fuera necesario.
/// </para>
///
/// <para>
/// <b>Convención de fechas</b>: se reciben como <see cref="DateTime"/> y
/// el controller las convierte a <see cref="DateOnly"/> si vienen con hora,
/// para que el SP reciba el tipo exacto de SQL <c>date</c> (sin componente
/// de tiempo). La conversion se hace en el controller porque la query
/// string siempre llega como string.
/// </para>
/// </summary>
public sealed record KardexFiltroInventarioDto
{
    /// <summary>Filtra por especialidad (ej: "Estructuras", "Instalaciones electricas").</summary>
    public int? IdEspecialidad { get; init; }

    /// <summary>Filtra por proyecto. NULL = traer tanto kardex con proyecto como sin proyecto.</summary>
    public int? IdProyecto { get; init; }

    /// <summary>Filtra por proveedor (solo aplica a KardexEntrada; KardexSalida lo ignora).</summary>
    public int? IdProveedor { get; init; }

    /// <summary>Fecha minima del movimiento (inclusive).</summary>
    public DateOnly? FechaDesde { get; init; }

    /// <summary>Fecha maxima del movimiento (inclusive).</summary>
    public DateOnly? FechaHasta { get; init; }
}
