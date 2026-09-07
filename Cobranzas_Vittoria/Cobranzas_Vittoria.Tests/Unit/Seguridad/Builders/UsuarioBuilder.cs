using Cobranzas_Vittoria.Seguridad.Domain.Model;

namespace Cobranzas_Vittoria.Tests.Unit.Seguridad.Builders;

/// <summary>
/// Builder de objetos <see cref="Usuario"/> para tests unitarios.
/// Expone valores por defecto razonables y permite sobreescribir solo
/// los campos relevantes para cada escenario.
///
/// Diseñado para evitar la duplicacion de datos de prueba y para ser
/// facil de extender cuando el modelo agregue nuevos campos.
/// </summary>
public sealed class UsuarioBuilder
{
    private int _idUsuario;
    private string _nombres = "Juan";
    private string _apellidos = "Perez";
    private string _correo = "juan.perez@local";
    private string _usuarioLogin = "jperez";
    private string _passwordHash = "password-hash";
    private bool _activo = true;
    private DateTime? _fechaCreacion;
    private string? _usuarioCreacion = "test-user";
    private List<Rol> _roles = new();

    public static UsuarioBuilder Nuevo() => new();

    public UsuarioBuilder ConId(int idUsuario)
    {
        _idUsuario = idUsuario;
        return this;
    }

    public UsuarioBuilder ConNombres(string nombres)
    {
        _nombres = nombres;
        return this;
    }

    public UsuarioBuilder ConApellidos(string apellidos)
    {
        _apellidos = apellidos;
        return this;
    }

    public UsuarioBuilder ConCorreo(string correo)
    {
        _correo = correo;
        return this;
    }

    public UsuarioBuilder ConUsuarioLogin(string usuarioLogin)
    {
        _usuarioLogin = usuarioLogin;
        return this;
    }

    public UsuarioBuilder ConPasswordHash(string passwordHash)
    {
        _passwordHash = passwordHash;
        return this;
    }

    public UsuarioBuilder Inactivo()
    {
        _activo = false;
        return this;
    }

    public UsuarioBuilder ConAuditoriaCreacion(DateTime fecha, string usuario)
    {
        _fechaCreacion = fecha;
        _usuarioCreacion = usuario;
        return this;
    }

    public UsuarioBuilder ConRol(Rol rol)
    {
        _roles.Add(rol);
        return this;
    }

    public UsuarioBuilder ConRoles(IEnumerable<Rol> roles)
    {
        _roles = roles.ToList();
        return this;
    }

    public Usuario BuildDomain()
    {
        var usuario = Usuario.Reconstruir(
            _idUsuario,
            _nombres,
            _apellidos,
            _correo,
            _usuarioLogin,
            _passwordHash,
            _activo,
            _usuarioCreacion ?? "test",
            _fechaCreacion ?? DateTime.UtcNow);

        if (_roles.Count > 0)
            usuario.AsignarRoles(_roles);

        return usuario;
    }

    public Usuario BuildNewDomain()
        => Usuario.Crear(
            _nombres,
            _apellidos,
            _correo,
            _usuarioLogin,
            _passwordHash,
            _usuarioCreacion ?? "test");
}
