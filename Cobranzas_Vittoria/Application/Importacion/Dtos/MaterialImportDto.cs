namespace Cobranzas_Vittoria.Application.Importacion.Dtos;

/// <summary>
/// DTO de importacion para la entidad <c>maestra.Material</c>.
///
/// El shape de este DTO coincide 1-a-1 con el TVP <c>maestra.TVP_Material</c>:
/// cada propiedad publica se mapea a una columna del TVP con el mismo nombre y
/// tipo compatible. El orden de las propiedades DEBE coincidir con el orden de
/// las columnas del TVP (el <c>TvpMapper</c> usa reflexion que respeta el orden
/// de declaracion).
///
/// La propiedad <c>_Fila</c> NO es una columna de la entidad destino; es el
/// numero de fila del archivo (1-based, sin contar la fila de encabezados).
///
/// Reglas de validacion (ejecutadas en el SP):
///   - <c>IdEspecialidad</c>: requerido, FK valida a maestra.Especialidad.
///   - <c>Codigo</c>: opcional, max 50 chars. Si llega NULL, el SP lo autogenera
///                    como 'MAT-' + correlativo (siguiendo el patron de
///                    usp_Material_Upsert). Unico intra-archivo y en BD cuando
///                    se proporciona.
///   - <c>Descripcion</c>: requerido, max 200 chars.
///   - <c>UnidadMedida</c>: requerido, max 30 chars. Es el codigo o nombre
///                           abreviado de la unidad (string libre).
///   - <c>StockMinimo</c>: opcional, default 0.
///   - <c>Activo</c>: requerido, default true.
///   - <c>IdUnidadMedida</c>: opcional, FK valida a maestra.UnidadMedida.
///   - <c>CodigoProveedor</c>: opcional, max 100 chars.
/// </summary>
public class MaterialImportDto
{
    /// <summary>FK a maestra.Especialidad. Requerido.</summary>
    public int IdEspecialidad { get; set; }

    /// <summary>Codigo del material. Opcional; si NULL, el SP lo autogenera como 'MAT-####'.</summary>
    public string? Codigo { get; set; }

    /// <summary>Descripcion del material. Requerido, max 200 chars.</summary>
    public string Descripcion { get; set; } = string.Empty;

    /// <summary>Unidad de medida (string libre, max 30 chars). Requerido.</summary>
    public string UnidadMedida { get; set; } = string.Empty;

    /// <summary>Stock minimo. Opcional, default 0.</summary>
    public decimal StockMinimo { get; set; } = 0m;

    /// <summary>Indica si el material esta activo. Default true.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>FK opcional a maestra.UnidadMedida.</summary>
    public int? IdUnidadMedida { get; set; }

    /// <summary>Codigo del proveedor (opcional, max 100 chars).</summary>
    public string? CodigoProveedor { get; set; }

    /// <summary>Numero de fila del archivo (1-based, sin contar encabezados). Metadata, no se persiste.</summary>
    public int _Fila { get; set; }
}
