using Cobranzas_Vittoria.Seguridad.Domain.Model;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;

/// <summary>
/// Builder de objetos <see cref="Rol"/> para tests unitarios.
/// Expone valores por defecto razonables y permite sobreescribir solo
/// los campos relevantes para cada escenario.
/// </summary>
public sealed class RolBuilder
{
    private int _idRol;
    private string _nombre = "Rol de prueba";
    private string _descripcion = "Descripcion de prueba";
    private bool _activo = true;
    private DateTime? _fechaCreacion;
    private string? _usuarioCreacion = "test-user";
    private DateTime? _fechaModificacion;
    private string? _usuarioModificacion;
    private List<Permiso> _permisos = new();

    public static RolBuilder Nuevo() => new();

    public RolBuilder ConId(int idRol)
    {
        _idRol = idRol;
        return this;
    }

    public RolBuilder ConNombre(string nombre)
    {
        _nombre = nombre;
        return this;
    }

    public RolBuilder ConDescripcion(string descripcion)
    {
        _descripcion = descripcion;
        return this;
    }

    public RolBuilder Inactivo()
    {
        _activo = false;
        return this;
    }

    public RolBuilder ConAuditoriaCreacion(DateTime fecha, string usuario)
    {
        _fechaCreacion = fecha;
        _usuarioCreacion = usuario;
        return this;
    }

    public RolBuilder ConAuditoriaModificacion(DateTime fecha, string usuario)
    {
        _fechaModificacion = fecha;
        _usuarioModificacion = usuario;
        return this;
    }

    public RolBuilder ConPermiso(Permiso permiso)
    {
        _permisos.Add(permiso);
        return this;
    }

    public RolBuilder ConPermisos(IEnumerable<Permiso> permisos)
    {
        _permisos = permisos.ToList();
        return this;
    }

    public Rol BuildDomain()
    {
        var rol = Rol.Reconstruir(
            _idRol,
            _nombre,
            _descripcion,
            _activo,
            _fechaCreacion,
            _usuarioCreacion,
            _fechaModificacion,
            _usuarioModificacion);

        if (_permisos.Count > 0)
            rol.EstablecerPermisos(_permisos);

        return rol;
    }

    public global::Cobranzas_Vittoria.Seguridad.Domain.Model.Rol BuildNewDomain()
        => global::Cobranzas_Vittoria.Seguridad.Domain.Model.Rol.Crear(_nombre, _descripcion);

    public global::Cobranzas_Vittoria.Seguridad.Domain.Model.Rol BuildNewDomainWithPermisos()
        => global::Cobranzas_Vittoria.Seguridad.Domain.Model.Rol.Crear(_nombre, _descripcion, _permisos);
}
