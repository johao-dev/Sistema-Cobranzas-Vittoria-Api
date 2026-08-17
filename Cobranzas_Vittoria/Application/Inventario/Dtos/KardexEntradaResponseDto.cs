namespace Cobranzas_Vittoria.Application.Inventario.Dtos;

/// <summary>
/// DTO de salida que representa una entrada de Kardex manual con sus joins
/// a maestra (Material, Especialidad, Proveedor, Proyecto). Se devuelve en
/// GET (lista y detalle), POST y PUT de KardexEntrada.
///
/// <para>
/// <b>Por que expone los nombres de las FK</b>:
/// el SP ya hace los INNER/LEFT JOIN a maestra y proyecta los nombres
/// legibles. Asi el controller no necesita hacer un segundo round-trip
/// para resolver cada nombre. Esto reduce la latencia de los listados.
/// </para>
///
/// <para>
/// <b>Convencion de nulabilidad</b>:
///   - <see cref="IdKardexEntrada"/>, <see cref="IdEspecialidad"/>,
///     <see cref="IdMaterial"/>, <see cref="Fecha"/>, <see cref="Cantidad"/>
///     y <see cref="FechaCreacion"/> son NOT NULL por DDL.
///   - <see cref="IdProveedor"/>, <see cref="IdProyecto"/>,
///     <see cref="NumeroDocumento"/>, <see cref="Observacion"/>,
///     <see cref="Especialidad"/>, <see cref="CodigoMaterial"/>,
///     <see cref="Nombre"/>, <see cref="Proveedor"/>, <see cref="Proyecto"/>
///     son NULL por DDL o por LEFT JOIN.
/// </para>
///
/// <para>
/// <b>Por que es una clase con <c>set</c></b>:
/// Dapper necesita poder hidratar las propiedades (es un DTO de salida,
/// no de entrada). Usar <c>init</c> rompe el mapping por reflection
/// que aplica Dapper por defecto.
/// </para>
/// </summary>
public sealed class KardexEntradaResponseDto
{
    public int IdKardexEntrada { get; set; }
    public int IdEspecialidad { get; set; }
    public string? Especialidad { get; set; }
    public int IdMaterial { get; set; }
    public string? CodigoMaterial { get; set; }
    public string? Nombre { get; set; }
    public int? IdProveedor { get; set; }
    public string? Proveedor { get; set; }
    public int? IdProyecto { get; set; }
    public string? Proyecto { get; set; }
    public string? NumeroDocumento { get; set; }
    public DateOnly Fecha { get; set; }
    public decimal Cantidad { get; set; }
    public string? Observacion { get; set; }
    public DateTime FechaCreacion { get; set; }
}
