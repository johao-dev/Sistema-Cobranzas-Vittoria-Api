namespace Cobranzas_Vittoria.Application.Importacion.Dtos;

/// <summary>
/// DTO de importacion para la entidad <c>maestra.Especialidad</c>.
///
/// El shape de este DTO coincide 1-a-1 con el TVP <c>maestra.TVP_Especialidad</c>:
/// cada propiedad publica se mapea a una columna del TVP con el mismo nombre y
/// tipo compatible. El orden de las propiedades DEBE coincidir con el orden de
/// las columnas del TVP (el <c>TvpMapper</c> usa reflexion que respeta el orden
/// de declaracion).
///
/// La propiedad <c>_Fila</c> NO es una columna de la entidad destino; es el
/// numero de fila del archivo (1-based, sin contar la fila de encabezados).
/// El processor la setea al construir el DTO para que el SP la incluya en el TVP
/// y el servicio pueda reportar errores con el contexto de fila correcto.
///
/// Reglas de validacion (ejecutadas en el SP):
///   - <c>Nombre</c>: requerido, max 100 caracteres. Unico intra-archivo y en BD.
///   - <c>Descripcion</c>: opcional, max 250 caracteres.
///   - <c>Activo</c>: requerido, default true.
/// </summary>
public class EspecialidadImportDto
{
    /// <summary>Nombre unico de la especialidad. Requerido, max 100 chars.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Descripcion opcional. Max 250 chars.</summary>
    public string? Descripcion { get; set; }

    /// <summary>Indica si la especialidad esta activa. Default true.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Numero de fila del archivo (1-based, sin contar encabezados). Metadata, no se persiste.</summary>
    public int _Fila { get; set; }
}
