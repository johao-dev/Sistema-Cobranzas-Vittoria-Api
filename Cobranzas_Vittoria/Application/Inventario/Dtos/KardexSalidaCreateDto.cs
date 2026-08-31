namespace Cobranzas_Vittoria.Application.Inventario.Dtos;

/// <summary>
/// DTO de entrada para crear o actualizar una salida manual de Kardex.
/// Contiene la cabecera + la lista de 1..N items.
///
/// <para>
/// <b>Items</b>: minimo uno (lo valida el SP con
/// <c>NOT EXISTS (SELECT 1 FROM @Items)</c>). El KardexInventarioValidator
/// hace la misma validacion a nivel de API para evitar el round-trip al SP.
/// </para>
///
/// <para>
/// <b>IdKardexSalida</b>: <c>int?</c> por la misma razon que
/// <c>IdKardexEntrada</c> en <see cref="KardexEntradaCreateDto"/>.
/// </para>
///
/// <para>
/// <b>Por que <c>List&lt;KardexSalidaItemCreateDto&gt;</c> y no <c>IList</c> o <c>IEnumerable</c></b>:
/// ASP.NET Core deserializa JSON arrays a <c>List&lt;T&gt;</c> por defecto.
/// Ademas, <c>List&lt;T&gt;</c> permite Count, indexer y mutation en tests.
/// </para>
/// </summary>
public sealed class KardexSalidaCreateDto
{
    /// <summary>PK. Null en POST; obligatorio en PUT (validado contra la ruta).</summary>
    public int? IdKardexSalida { get; set; }

    /// <summary>FK a maestra.Especialidad (REQUERIDO).</summary>
    public int IdEspecialidad { get; set; }

    /// <summary>FK a maestra.Proyecto (REQUERIDO). Etiqueta informativa; no segmenta stock.</summary>
    public int? IdProyecto { get; set; }

    /// <summary>Numero de documento soporte, ej: "S001-12345" (OPCIONAL, max 50 chars).</summary>
    public string? NumeroDocumento { get; set; }

    /// <summary>Fecha del movimiento (REQUERIDO).</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Nombre de quien solicita la salida (REQUERIDO, no vacio, max 150 chars).</summary>
    public string Solicitante { get; set; } = string.Empty;

    /// <summary>Observacion general (OPCIONAL, max 250 chars).</summary>
    public string? Observacion { get; set; }

    /// <summary>Lista de 1..N items de la salida. REQUERIDO, no vacia.</summary>
    public List<KardexSalidaItemCreateDto> Items { get; set; } = new();
}
