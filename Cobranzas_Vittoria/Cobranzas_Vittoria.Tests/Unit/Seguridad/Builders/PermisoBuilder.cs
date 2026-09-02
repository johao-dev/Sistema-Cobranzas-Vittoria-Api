using Cobranzas_Vittoria.Seguridad.Domain.Model;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;

/// <summary>
/// Builder de objetos <see cref="Permiso"/> para tests unitarios.
/// Expone valores por defecto razonables y permite sobreescribir solo
/// los campos relevantes para cada escenario.
///
/// Diseñado para evitar la duplicacion de datos de prueba y para ser
/// facil de extender cuando el modelo agregue nuevos campos (por ejemplo,
/// permisos agrupados por modulo, iconos, etc.).
/// </summary>
public sealed class PermisoBuilder
{
    private int _idPermiso;
    private string _codigo = "permiso.test";
    private string _nombre = "Permiso de prueba";
    private string _descripcion = "Descripcion de prueba";
    private bool _activo = true;
    private DateTime? _fechaCreacion;
    private string? _usuarioCreacion = "test-user";
    private DateTime? _fechaModificacion;
    private string? _usuarioModificacion;

    public static PermisoBuilder Nuevo() => new();

    public PermisoBuilder ConId(int idPermiso)
    {
        _idPermiso = idPermiso;
        return this;
    }

    public PermisoBuilder ConCodigo(string codigo)
    {
        _codigo = codigo;
        return this;
    }

    public PermisoBuilder ConNombre(string nombre)
    {
        _nombre = nombre;
        return this;
    }

    public PermisoBuilder ConDescripcion(string descripcion)
    {
        _descripcion = descripcion;
        return this;
    }

    public PermisoBuilder Inactivo()
    {
        _activo = false;
        return this;
    }

    public PermisoBuilder ConAuditoriaCreacion(DateTime fecha, string usuario)
    {
        _fechaCreacion = fecha;
        _usuarioCreacion = usuario;
        return this;
    }

    public PermisoBuilder ConAuditoriaModificacion(DateTime fecha, string usuario)
    {
        _fechaModificacion = fecha;
        _usuarioModificacion = usuario;
        return this;
    }

    public Permiso BuildDomain()
        => Permiso.Reconstruir(
            _idPermiso,
            _codigo,
            _nombre,
            _descripcion,
            _activo,
            _fechaCreacion,
            _usuarioCreacion,
            _fechaModificacion,
            _usuarioModificacion);

    public Permiso BuildNewDomain()
        => Permiso.Crear(_codigo, _nombre, _descripcion);
}
