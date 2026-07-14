namespace Cobranzas_Vittoria.Application.Importacion.Dtos;

/// <summary>
/// DTO de importacion para la entidad <c>maestra.UnidadMedida</c>.
///
/// El shape de este DTO coincide 1-a-1 con el TVP <c>maestra.TVP_UnidadMedida</c>:
/// cada propiedad publica se mapea a una columna del TVP con el mismo nombre y
/// tipo compatible. El <c>TvpMapper</c> usa reflexion para hacer el mapeo.
///
/// La propiedad <c>_Fila</c> NO es una columna de la entidad destino; es el
/// numero de fila del archivo (1-based, sin contar la fila de encabezados).
/// El processor la setea al construir el DTO para que el SP la incluya en el TVP
/// y el servicio pueda reportar errores con el contexto de fila correcto.
///
/// Reglas de validacion:
///   - <c>Codigo</c>: requerido, max 20 caracteres (match con el TVP y la tabla destino)
///   - <c>Nombre</c>: requerido, max 100 caracteres
///   - <c>Activo</c>: requerido, default true
///
/// Las validaciones de obligatoriedad, longitud, duplicados y existencia previa
/// se ejecutan en el SP <c>maestra.usp_UnidadMedida_CargaMasiva</c> dentro de una
/// transaccion. Si la API necesita pre-validar (Fase 4), puede usar DataAnnotations
/// o FluentValidation sobre este mismo DTO.
/// </summary>
public class UnidadMedidaImportDto
{
    /// <summary>Codigo unico de la unidad de medida. Requerido, max 20 chars.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Nombre descriptivo. Requerido, max 100 chars.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Indica si la unidad esta activa. Default true (las nuevas se crean activas).</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Numero de fila del archivo (1-based, sin contar encabezados). Metadata, no se persiste.</summary>
    public int _Fila { get; set; }
}
