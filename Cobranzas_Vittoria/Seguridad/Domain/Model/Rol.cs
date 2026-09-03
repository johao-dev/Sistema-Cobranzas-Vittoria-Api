using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Domain.Model;

public class Rol
{
    public int IdRol { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public bool Activo { get; private set; }
    
    public DateTime? FechaCreacion { get; private set; }
    public string? UsuarioCreacion { get; private set; } = string.Empty;
    public DateTime? FechaModificacion { get; private set; }
    public string? UsuarioModificacion { get; private set; } = string.Empty;

    // Colección de permisos asociados al rol.
    private readonly Dictionary<string, Permiso> _permisos = new();
    public IEnumerable<Permiso> Permisos => _permisos.Values;

    private Rol() { }

    /// <summary>
    /// Crea una nueva instancia de un rol con el nombre y la descripción especificados.
    /// </summary>
    /// <param name="nombre">El nombre del rol.</param>
    /// <param name="descripcion">La descripción del rol.</param>
    /// 
    /// <returns>Una nueva instancia de <see cref="Rol"/> con los valores especificados.</returns>
    public static Rol Crear(string nombre, string descripcion)
    {
        ValidarNombre(nombre);

        return new Rol
        {
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim() ?? string.Empty,
            Activo = true,
        };
    }

    /// <summary>
    /// Crea una nueva instancia de un rol con el nombre, la descripción y los permisos especificados.
    /// </summary>
    /// <param name="nombre">El nombre del rol.</param>
    /// <param name="descripcion">La descripción del rol.</param>
    /// <param name="permisos">La colección de permisos que se desea asociar al rol.</param>
    /// <returns>Una nueva instancia de <see cref="Rol"/> con los valores especificados.</returns>
    public static Rol Crear(string nombre, string descripcion, IEnumerable<Permiso> permisos)
    {
        ValidarNombre(nombre);
        var rol = new Rol
        {
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim() ?? string.Empty,
            Activo = true,
        };
        rol.EstablecerPermisos(permisos);
        return rol;
    }

    /// <summary>
    /// Actualiza el nombre y la descripción del rol. También establece la fecha de modificación a la
    /// fecha y hora actual (UTC).
    /// </summary>
    /// <param name="nombre">El nuevo nombre del rol.</param>
    /// <param name="descripcion">La nueva descripción del rol.</param>
    public void ActualizarDatos(string nombre, string descripcion)
    {
        ValidarNombre(nombre);

        Nombre = nombre.Trim();
        Descripcion = descripcion?.Trim() ?? string.Empty;
        FechaModificacion = DateTime.UtcNow;
    }

    private static void ValidarNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(Nombre),
                "ROL_NOMBRE_REQUERIDO",
                "El nombre del rol es requerido.");
        }
    }

    /// <summary>
    /// Agrega un permiso al rol. Si el permiso ya existe, no se agrega.
    /// 
    /// </summary>
    /// <param name="permiso">El permiso que se desea agregar al rol.</param>
    /// <exception cref="ArgumentNullException">Se lanza si el permiso es nulo.</exception>
    /// <returns>True si el permiso fue agregado correctamente; de lo contrario, false.</returns>
    public bool AgregarPermiso(Permiso permiso)
    {
        ArgumentNullException.ThrowIfNull(permiso);
        return _permisos.TryAdd(permiso.Codigo, permiso);
    }

    /// <summary>
    /// Establece los permisos del rol, reemplazando cualquier permiso existente.
    /// <para>
    /// <b>Nota:</b> El método reemplaza todos los permisos existentes del rol con los nuevos permisos proporcionados.
    /// Usar este método solo para la reconstrucción de roles desde una fuente de datos persistente.
    /// </para>
    /// </summary>
    /// <param name="permisos">La colección de permisos que se desea establecer en el rol.</param>
    /// <exception cref="ArgumentNullException">Se lanza si la colección de permisos es nula.</exception>
    public void EstablecerPermisos(IEnumerable<Permiso> permisos)
    {
        ArgumentNullException.ThrowIfNull(permisos);
        _permisos.Clear();
        foreach (var permiso in permisos)
        {
            AgregarPermiso(permiso);
        }
    }

    /// <summary>
    /// Quita un permiso del rol. Si el permiso no existe, no se realiza ninguna acción.
    /// </summary>
    /// <param name="permiso">El permiso que se desea quitar del rol.</param>
    /// <exception cref="ArgumentNullException">Se lanza si el permiso es nulo.</exception>
    /// <returns>True si el permiso fue quitado correctamente; de lo contrario, false.</returns>
    public bool QuitarPermiso(Permiso permiso)
    {
        ArgumentNullException.ThrowIfNull(permiso);
        return _permisos.Remove(permiso.Codigo);
    }

    /// <summary>
    /// Limpia todos los permisos del rol.
    /// <para>Este método elimina todos los permisos asociados al rol. Cuidado al usarlo, ya que no se puede deshacer.</para>
    /// </summary>
    public void LimpiarPermisos()
    {
        _permisos.Clear();
    }

    /// <summary>
    /// Establece la información de auditoría de creación del rol.
    /// </summary>
    /// <param name="usuarioCreacion">El usuario que creó el rol.</param>
    public void EstablecerAuditoriaCreacion(string usuarioCreacion)
    {
        FechaCreacion = DateTime.UtcNow;
        UsuarioCreacion = usuarioCreacion;
    }

    /// <summary>
    /// Establece la información de auditoría de modificación del rol.
    /// </summary>
    /// <param name="usuarioModificacion">El usuario que modificó el rol.</param>
    public void EstablecerAuditoriaModificacion(string usuarioModificacion)
    {
        FechaModificacion = DateTime.UtcNow;
        UsuarioModificacion = usuarioModificacion;
    }

    /// <summary>
    /// Reconstruye un rol a partir de los datos proporcionados.
    /// No restaura los permisos asociados al rol.
    /// </summary>
    /// <param name="idRol"></param>
    /// <param name="nombre"></param>
    /// <param name="descripcion"></param>
    /// <param name="activo"></param>
    /// <param name="fechaCreacion"></param>
    /// <param name="usuarioCreacion"></param>
    /// <param name="fechaModificacion"></param>
    /// <param name="usuarioModificacion"></param>
    /// <returns></returns>
    public static Rol Reconstruir(
        int idRol,
        string nombre,
        string descripcion,
        bool activo,
        DateTime? fechaCreacion = null,
        string? usuarioCreacion = null,
        DateTime? fechaModificacion = null,
        string? usuarioModificacion = null)
    {
        return new Rol
        {
            IdRol = idRol,
            Nombre = nombre,
            Descripcion = descripcion,
            Activo = activo,
            FechaCreacion = fechaCreacion,
            UsuarioCreacion = usuarioCreacion,
            FechaModificacion = fechaModificacion,
            UsuarioModificacion = usuarioModificacion,
        };
    }
}
