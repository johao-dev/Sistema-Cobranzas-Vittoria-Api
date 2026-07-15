namespace Cobranzas_Vittoria.Application.Importacion.Dtos;

/// <summary>
/// DTO de importacion para la entidad <c>maestra.CategoriaGasto</c>.
///
/// El shape de este DTO coincide 1-a-1 con el TVP <c>maestra.TVP_CategoriaGasto</c>:
/// cada propiedad publica se mapea a una columna del TVP con el mismo nombre y
/// tipo compatible. El orden de las propiedades DEBE coincidir con el orden de
/// las columnas del TVP (el <c>TvpMapper</c> usa reflexion que respeta el orden
/// de declaracion).
///
/// La propiedad <c>_Fila</c> NO es una columna de la entidad destino; es el
/// numero de fila del archivo (1-based, sin contar la fila de encabezados).
///
/// Reglas de validacion (ejecutadas en el SP):
///   - <c>Nombre</c>: requerido, max 150 chars, unico intra-archivo y en BD.
///   - <c>Activo</c>: requerido, default true.
/// </summary>
public class CategoriaGastoImportDto
{
    /// <summary>Nombre de la categoria de gasto. Requerido, max 150 chars, unico en BD.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Indica si la categoria esta activa. Default true.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Numero de fila del archivo (1-based, sin contar encabezados). Metadata, no se persiste.</summary>
    public int _Fila { get; set; }
}
