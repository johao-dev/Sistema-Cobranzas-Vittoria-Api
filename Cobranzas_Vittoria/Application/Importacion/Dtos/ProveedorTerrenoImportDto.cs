namespace Cobranzas_Vittoria.Application.Importacion.Dtos;

/// <summary>
/// DTO de importacion para la entidad <c>maestra.ProveedorTerreno</c>.
///
/// El shape de este DTO coincide 1-a-1 con el TVP <c>maestra.TVP_ProveedorTerreno</c>:
/// cada propiedad publica se mapea a una columna del TVP con el mismo nombre y
/// tipo compatible. El orden de las propiedades DEBE coincidir con el orden de
/// las columnas del TVP (el <c>TvpMapper</c> usa reflexion que respeta el orden
/// de declaracion).
///
/// La propiedad <c>_Fila</c> NO es una columna de la entidad destino; es el
/// numero de fila del archivo (1-based, sin contar la fila de encabezados).
///
/// Reglas de validacion (ejecutadas en el SP):
///   - <c>RazonSocial</c>: requerido, max 250 chars, unica intra-archivo y en BD.
///   - Resto de campos: opcionales.
/// </summary>
public class ProveedorTerrenoImportDto
{
    /// <summary>Razon social del proveedor. Requerido, max 250 chars, unica en BD.</summary>
    public string RazonSocial { get; set; } = string.Empty;

    /// <summary>RUC del proveedor. Opcional, max 20 chars.</summary>
    public string? Ruc { get; set; }

    /// <summary>Persona de contacto. Opcional, max 150 chars.</summary>
    public string? Contacto { get; set; }

    /// <summary>Telefono de contacto. Opcional, max 50 chars.</summary>
    public string? Telefono { get; set; }

    /// <summary>Correo electronico. Opcional, max 150 chars.</summary>
    public string? Correo { get; set; }

    /// <summary>Indica si el proveedor esta activo. Default true.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Numero de fila del archivo (1-based, sin contar encabezados). Metadata, no se persiste.</summary>
    public int _Fila { get; set; }
}
