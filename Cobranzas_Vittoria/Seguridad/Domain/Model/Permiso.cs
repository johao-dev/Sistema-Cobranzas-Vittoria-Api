using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Domain.Model;

/// <summary>
/// Representa un permiso del sistema.
/// </summary>
public class Permiso : IEquatable<Permiso>
{
    public int IdPermiso { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public bool Activo { get; private set; } = true;

    public DateTime? FechaCreacion { get; private set; }
    public string? UsuarioCreacion { get; private set; }
    public DateTime? FechaModificacion { get; private set; }
    public string? UsuarioModificacion { get; private set; }

    private Permiso() { }

    /// <summary>
    /// Crea un permiso valido y lanza una excepcion de dominio si se violan
    /// las reglas de negocio basicas.
    /// </summary>
    public static Permiso Crear(string codigo, string nombre, string descripcion)
    {
        ValidarCodigo(codigo);
        ValidarNombre(nombre);

        return new Permiso
        {
            Codigo = codigo.Trim(),
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim() ?? string.Empty,
            Activo = true
        };
    }

    /// <summary>
    /// Actualiza solo los campos editables del permiso: Nombre y Descripcion.
    /// El Codigo no se puede cambiar una vez creado.
    /// </summary>
    public void ActualizarDatos(string nombre, string descripcion)
    {
        ValidarNombre(nombre);

        Nombre = nombre.Trim();
        Descripcion = descripcion?.Trim() ?? string.Empty;
        FechaModificacion = DateTime.UtcNow;
    }

    /// <summary>
    /// Establece el usuario y fecha de creacion. Se invoca desde el caso de uso
    /// justo antes de persistir.
    /// </summary>
    public void EstablecerAuditoriaCreacion(string usuarioCreacion)
    {
        FechaCreacion = DateTime.UtcNow;
        UsuarioCreacion = usuarioCreacion;
    }

    /// <summary>
    /// Establece el usuario y fecha de modificacion. Se invoca desde el caso de uso
    /// justo antes de persistir.
    /// </summary>
    public void EstablecerAuditoriaModificacion(string usuarioModificacion)
    {
        FechaModificacion = DateTime.UtcNow;
        UsuarioModificacion = usuarioModificacion;
    }

    public void Desactivar()
    {
        Activo = false;
        FechaModificacion = DateTime.UtcNow;
    }

    public void Activar()
    {
        Activo = true;
        FechaModificacion = DateTime.UtcNow;
    }

    /// <summary>
    /// Reconstruye la entidad desde persistencia sin revalidar.
    ///
    /// <para>
    /// <b>Nota:</b> Usar solo en mappers/repositorios.
    /// </para>
    /// </summary>
    public static Permiso Reconstruir(
        int idPermiso,
        string codigo,
        string nombre,
        string descripcion,
        bool activo,
        DateTime? fechaCreacion = null,
        string? usuarioCreacion = null,
        DateTime? fechaModificacion = null,
        string? usuarioModificacion = null)
    {
        return new Permiso
        {
            IdPermiso = idPermiso,
            Codigo = codigo,
            Nombre = nombre,
            Descripcion = descripcion,
            Activo = activo,
            FechaCreacion = fechaCreacion,
            UsuarioCreacion = usuarioCreacion,
            FechaModificacion = fechaModificacion,
            UsuarioModificacion = usuarioModificacion
        };
    }

    private static void ValidarCodigo(string codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(Codigo),
                "PERMISO_CODIGO_REQUERIDO",
                "El codigo del permiso es requerido.");
        }

        if (codigo.Contains(' '))
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(Codigo),
                "PERMISO_CODIGO_ESPACIOS",
                "El codigo del permiso no puede contener espacios.");
        }
    }

    private static void ValidarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(Nombre),
                "PERMISO_NOMBRE_REQUERIDO",
                "El nombre del permiso es requerido.");
        }
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as Permiso);
    }

    public bool Equals(Permiso? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(Codigo, other.Codigo, StringComparison.OrdinalIgnoreCase);
    }

    public override int GetHashCode()
    {
        return Codigo is null
            ? 0
            : StringComparer.OrdinalIgnoreCase.GetHashCode(Codigo);
    }
}
