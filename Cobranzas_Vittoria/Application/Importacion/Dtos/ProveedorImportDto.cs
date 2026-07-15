namespace Cobranzas_Vittoria.Application.Importacion.Dtos;

/// <summary>
/// DTO de importacion para la entidad <c>maestra.Proveedor</c>.
///
/// El shape de este DTO coincide 1-a-1 con el TVP <c>maestra.TVP_Proveedor</c>:
/// cada propiedad publica se mapea a una columna del TVP con el mismo nombre y
/// tipo compatible. El orden de las propiedades DEBE coincidir con el orden de
/// las columnas del TVP (el <c>TvpMapper</c> usa reflexion que respeta el orden
/// de declaracion).
///
/// La propiedad <c>_Fila</c> NO es una columna de la entidad destino; es el
/// numero de fila del archivo (1-based, sin contar la fila de encabezados).
///
/// Reglas de validacion (ejecutadas en el SP):
///   - <c>RazonSocial</c>: requerido, max 200 chars.
///   - <c>Ruc</c>: requerido, max 20 chars, unico intra-archivo y en BD.
///   - Resto de campos: opcionales.
/// </summary>
public class ProveedorImportDto
{
    /// <summary>Razon social del proveedor. Requerido, max 200 chars.</summary>
    public string RazonSocial { get; set; } = string.Empty;

    /// <summary>RUC del proveedor. Requerido, max 20 chars, unico en BD.</summary>
    public string Ruc { get; set; } = string.Empty;

    /// <summary>Persona de contacto. Opcional, max 150 chars.</summary>
    public string? Contacto { get; set; }

    /// <summary>Telefono de contacto. Opcional, max 30 chars.</summary>
    public string? Telefono { get; set; }

    /// <summary>Correo electronico. Opcional, max 150 chars.</summary>
    public string? Correo { get; set; }

    /// <summary>Direccion fiscal. Opcional, max 250 chars.</summary>
    public string? Direccion { get; set; }

    /// <summary>Banco del proveedor. Opcional, max 50 chars.</summary>
    public string? Banco { get; set; }

    /// <summary>Cuenta corriente. Opcional, max 50 chars.</summary>
    public string? CuentaCorriente { get; set; }

    /// <summary>CCI (codigo de cuenta interbancaria). Opcional, max 50 chars.</summary>
    public string? CCI { get; set; }

    /// <summary>Cuenta de detraccion. Opcional, max 50 chars.</summary>
    public string? CuentaDetraccion { get; set; }

    /// <summary>Descripcion del servicio que brinda. Opcional, max 250 chars.</summary>
    public string? DescripcionServicio { get; set; }

    /// <summary>Observaciones libres. Opcional, max 250 chars.</summary>
    public string? Observacion { get; set; }

    /// <summary>Indicador de si trabajamos con el proveedor (ej. "SI"/"NO"). Opcional, max 10 chars.</summary>
    public string? TrabajamosConProveedor { get; set; }

    /// <summary>Indica si el proveedor esta activo. Default true.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Numero de fila del archivo (1-based, sin contar encabezados). Metadata, no se persiste.</summary>
    public int _Fila { get; set; }
}
