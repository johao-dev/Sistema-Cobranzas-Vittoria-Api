namespace Cobranzas_Vittoria.Seguridad.Domain.Model;

/// <summary>
/// Representa un permiso del sistema.
/// </summary>
public class Permiso
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
    /// Crea un permiso valido y lanza excepciones de
    /// aplicacion si se violan las reglas de negocio basicas.
    /// </summary>
    public static Permiso Crear(string codigo, string nombre, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("El codigo del permiso es requerido.", nameof(codigo));

        if (codigo.Contains(' '))
            throw new ArgumentException("El codigo del permiso no puede contener espacios.", nameof(codigo));

        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del permiso es requerido.", nameof(nombre));

        return new Permiso
        {
            Codigo = codigo.Trim(),
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim() ?? string.Empty,
            Activo = true
        };
    }

    /// <summary>
    /// Permite actualizar datos editables del permiso.
    /// </summary>
    public void ActualizarDatos(string nombre, string descripcion)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre del permiso es requerido.", nameof(nombre));

        Nombre = nombre.Trim();
        Descripcion = descripcion?.Trim() ?? string.Empty;
        FechaModificacion = DateTime.UtcNow;
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
}
