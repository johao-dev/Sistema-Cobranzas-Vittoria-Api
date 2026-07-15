namespace Cobranzas_Vittoria.Application.Importacion.Processors;

/// <summary>
/// Resultado de una importacion masiva exitosa.
///
/// Se devuelve en la respuesta HTTP 200 con la cantidad de filas insertadas y
/// el formato del archivo origen (util para logging y diagnostico).
/// </summary>
/// <param name="Modulo">
/// Identificador del modulo importado (ej: "unidad-medida", "especialidad").
/// Coincide con el segmento de URL <c>/api/import/{modulo}</c>.
/// </param>
/// <param name="Formato">Formato del archivo: "csv", "xlsx" o "xls".</param>
/// <param name="FilasInsertadas">Cantidad de filas efectivamente insertadas en BD.</param>
public sealed record ResultadoImportacion(
    string Modulo,
    string Formato,
    int FilasInsertadas);
