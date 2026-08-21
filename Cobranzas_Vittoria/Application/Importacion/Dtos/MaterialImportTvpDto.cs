namespace Cobranzas_Vittoria.Application.Importacion.Dtos;

/// <summary>
/// DTO de TVP (C# -&gt; BD) para la carga masiva de <c>maestra.Material</c>.
///
/// El shape de este DTO coincide 1-a-1 con el TVP <c>maestra.TVP_Material_v2</c>:
/// cada propiedad publica se mapea a una columna del TVP con el mismo nombre y
/// tipo compatible. El orden de las propiedades DEBE coincidir con el orden
/// de las columnas del TVP (el <c>TvpMapper</c> usa reflexion que respeta el
/// orden de declaracion).
///
/// Se diferencia de <see cref="MaterialImportDto"/> en que:
///   - <see cref="IdEspecialidad"/> reemplaza al string Especialidad: el
///     processor ya lo resolvio contra la BD (creando el catalogo si hacia
///     falta) y carga aqui el FK.
///   - <see cref="IdUnidadMedida"/> se agrega como FK opcional. El texto
///     <see cref="UnidadMedida"/> se sigue persistiendo en la columna de
///     <c>maestra.Material.UnidadMedida</c> (max 30 chars) por motivos de
///     trazabilidad: si en el futuro el catalogo se renombra o elimina, el
///     registro historico conserva su unidad original.
///
/// La propiedad <c>_Fila</c> NO es una columna de la entidad destino; es el
/// numero de fila del archivo (1-based, sin contar la fila de encabezados).
/// </summary>
public class MaterialImportTvpDto
{
    /// <summary>FK a maestra.Especialidad. Resuelto por el processor (creada en transaccion si no existia).</summary>
    public int IdEspecialidad { get; set; }

    /// <summary>Codigo del material. Requerido, max 50 chars. Lo trae el usuario; el SP NO lo autogenera.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Descripcion del material. Requerido, max 200 chars.</summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>FK opcional a maestra.UnidadMedida. Resuelto por el processor; null si la UnidadMedida original no se pudo crear.</summary>
    public int? IdUnidadMedida { get; set; }

    /// <summary>Unidad de medida (texto libre del archivo, max 30 chars). Requerido. Se persiste tal cual en Material.UnidadMedida.</summary>
    public string UnidadMedida { get; set; } = string.Empty;

    /// <summary>Numero de fila del archivo (1-based, sin contar encabezados). Metadata, no se persiste.</summary>
    public int _Fila { get; set; }
}
