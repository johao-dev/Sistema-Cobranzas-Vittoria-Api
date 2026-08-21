namespace Cobranzas_Vittoria.Application.Importacion.Dtos;

/// <summary>
/// DTO de importacion para la nueva plantilla amigable de <c>maestra.Material</c>.
///
/// El archivo de entrada tiene 4 encabezados visibles para el usuario:
///   - <c>Especialidad</c>   (string, requerido)
///   - <c>Nombre</c>         (string, requerido, se mapea a Descripcion)
///   - <c>UnidadMedida</c>   (string, requerido)
///   - <c>Codigo</c>         (string, requerido)
///
/// A diferencia de la v1, este DTO NO contiene IDs de FKs. Los catalogos
/// (Especialidad, UnidadMedida) se resuelven en
/// <see cref="Processors.MaterialImportProcessor"/>
/// dentro de la misma transaccion, antes de invocar el SP. La conversion a
/// IDs se hace en <c>MaterialImportTvpDto</c> (DTO de TVP).
///
/// La propiedad <c>_Fila</c> NO es una columna de la entidad destino; es el
/// numero de fila del archivo (1-based, sin contar la fila de encabezados).
///
/// Reglas de validacion (ejecutadas por el processor y/o el SP):
///   - <c>Especialidad</c>: requerido, no vacio. Se busca en BD
///     (case-insensitive, accent-insensitive). Si no existe, se crea en la
///     misma transaccion.
///   - <c>Nombre</c>:       requerido, no vacio, max 200 chars. Se mapea a
///                          <c>maestra.Material.Descripcion</c>.
///   - <c>UnidadMedida</c>: requerido, no vacio, max 30 chars. Se busca en
///                          BD; si no existe, se crea con codigo
///                          autogenerado "UM-<sigla>-####" en la misma
///                          transaccion.
///   - <c>Codigo</c>:       requerido, no vacio, max 50 chars. Lo trae el
///                          usuario; el sistema NO lo autogenera.
/// </summary>
public class MaterialImportDto
{
    /// <summary>Nombre de la Especialidad. Requerido, no vacio. Se resuelve a IdEspecialidad antes de invocar el SP.</summary>
    public string Especialidad { get; set; } = string.Empty;

    /// <summary>Descripcion del material (encabezado "Nombre" en el archivo). Requerido, max 200 chars.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Unidad de medida (texto libre, max 30 chars). Requerido. Se resuelve a IdUnidadMedida antes del SP; el texto se persiste tal cual en Material.UnidadMedida.</summary>
    public string UnidadMedida { get; set; } = string.Empty;

    /// <summary>Codigo del material. Requerido, no vacio, max 50 chars. Lo trae el usuario; el sistema NO lo autogenera.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Numero de fila del archivo (1-based, sin contar encabezados). Metadata, no se persiste.</summary>
    public int _Fila { get; set; }
}
