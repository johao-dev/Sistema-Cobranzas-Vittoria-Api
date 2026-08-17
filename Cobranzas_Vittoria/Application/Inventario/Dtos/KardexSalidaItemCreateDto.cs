namespace Cobranzas_Vittoria.Application.Inventario.Dtos;

/// <summary>
/// DTO que representa un item de KardexSalida (1..N por salida).
///
/// <para>
/// <b>Mapeo a TVP</b>: las propiedades publicas de este DTO se mapean 1-a-1
/// con las columnas del TVP <c>almacen.TVP_KardexSalidaItem</c>
/// (IdMaterial, Cantidad, Observacion). El orden de declaracion es
/// importante porque <c>TvpMapper</c> respeta el orden de las propiedades.
/// </para>
///
/// <para>
/// <b>Por que se mantiene como DTO independiente (no se reutiliza
/// <c>KardexEntradaCreateDto</c>)</b>: KardexEntrada y KardexSalida tienen
/// campos distintos (IdMaterial + Cantidad viven en la cabecera de entrada,
/// pero en el detalle de salida). Reutilizar forzaria un DTO con campos
/// nulos para uno de los dos casos, lo que oculta el modelo de negocio.
/// </para>
///
/// <para>
/// <b>Cantidad</b>: el SP exige <c>&gt;= 0</c>. Una salida con cantidad 0
/// es valida (caso de ajuste / anulacion).
/// </para>
///
/// <para>
/// <b>Clase mutable con <c>set</c></b>: por la misma razon que
/// <see cref="KardexSalidaCreateDto"/>, es la forma estandar del proyecto
/// para DTOs bindable.
/// </para>
/// </summary>
public sealed class KardexSalidaItemCreateDto
{
    /// <summary>FK a maestra.Material (REQUERIDO).</summary>
    public int IdMaterial { get; set; }

    /// <summary>Cantidad despachada (REQUERIDO, &gt;= 0).</summary>
    public decimal Cantidad { get; set; }

    /// <summary>Observacion del item (OPCIONAL, max 250 chars).</summary>
    public string? Observacion { get; set; }
}
