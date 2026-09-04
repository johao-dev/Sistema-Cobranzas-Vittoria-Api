using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.ValueObject;

namespace Cobranzas_Vittoria.Seguridad.Domain.Model;

public class Usuario
{
    public int IdUsuario { get; private set; }
    public string Nombres { get; private set; } = string.Empty;
    public string Apellidos { get; private set; } = string.Empty;
    public Email Correo { get; private set; } = null!;
    public string UsuarioLogin { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool Activo { get; private set; }

    public DateTime? FechaCreacion { get; private set; }
    public string? UsuarioCreacion { get; private set; } = string.Empty;

    private readonly Dictionary<string, Rol> _roles = new();
    public IEnumerable<Rol> Roles => _roles.Values;

    private Usuario() { }

    public static Usuario Crear(
        string nombres,
        string apellidos,
        string correo,
        string usuarioLogin,
        string passwordHash,
        string usuarioCreacion)
    {
        ValidarNombreCompleto(nombres, apellidos);

        var usuario = new Usuario
        {
            Nombres = nombres.Trim(),
            Apellidos = apellidos.Trim(),
            Correo = new Email(correo),
            Activo = true,
        };

        usuario.AsignarCredenciales(usuarioLogin, passwordHash);
        usuario.EstablecerAuditoriaCreacion(usuarioCreacion);

        return usuario;
    }

    private static void ValidarNombreCompleto(string nombres, string apellidos)
    {
        if (string.IsNullOrWhiteSpace(nombres))
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(Nombres),
                "USUARIO_NOMBRE_REQUERIDO",
                "El nombre del usuario es requerido.");
        }

        if (string.IsNullOrWhiteSpace(apellidos))
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(Apellidos),
                "USUARIO_APELLIDO_REQUERIDO",
                "El apellido del usuario es requerido.");
        }
    }

    public void AsignarCredenciales(string usuarioLogin, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(usuarioLogin))
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(UsuarioLogin),
                "USUARIO_LOGIN_REQUERIDO",
                "El usuario de login es requerido.");
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(PasswordHash),
                "USUARIO_PASSWORD_REQUERIDO",
                "La contraseña es requerida.");
        }

        UsuarioLogin = usuarioLogin.Trim();
        PasswordHash = passwordHash;
    }

    public void ActualizarDatos(string nombres, string apellidos)
    {
        ValidarNombreCompleto(nombres, apellidos);

        Nombres = nombres.Trim();
        Apellidos = apellidos.Trim();
    }

    public void ActualizarCorreo(string correo)
    {
        Correo = new Email(correo);
    }

    public void EstablecerAuditoriaCreacion(string usuarioCreacion)
    {
        if (string.IsNullOrWhiteSpace(usuarioCreacion))
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(UsuarioCreacion),
                "USUARIO_AUDITORIA_CREACION_REQUERIDO",
                "El usuario de creación es requerido.");
        }

        UsuarioCreacion = usuarioCreacion;
        FechaCreacion = DateTime.UtcNow;
    }

    public bool TieneRol(string nombreRol)
    {
        ArgumentNullException.ThrowIfNull(nombreRol);

        if (string.IsNullOrWhiteSpace(nombreRol))
        {
            throw new ArgumentException(
                "El nombre del rol no puede estar vacío.",
                nameof(nombreRol));
        }

        return _roles.ContainsKey(nombreRol);
    }

    public void AsignarRoles(IEnumerable<Rol> roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

        _roles.Clear();

        foreach (var rol in roles)
        {
            AsignarRol(rol);
        }
    }

    public bool AsignarRol(Rol rol)
    {
        ArgumentNullException.ThrowIfNull(rol);
        return _roles.TryAdd(rol.Nombre, rol);
    }

    public bool QuitarRol(Rol rol)
    {
        ArgumentNullException.ThrowIfNull(rol);
        return _roles.Remove(rol.Nombre);
    }

    public void Activar()
    {
        Activo = true;
    }

    public void Desactivar()
    {
        Activo = false;
    }

    public static Usuario Reconstruir(
        int idUsuario,
        string nombres,
        string apellidos,
        string correo,
        string usuarioLogin,
        string passwordHash,
        bool activo,
        string usuarioCreacion,
        DateTime fechaCreacion)
    {
        var usuario = new Usuario
        {
            IdUsuario = idUsuario,
            Nombres = nombres,
            Apellidos = apellidos,
            Correo = new Email(correo),
            UsuarioLogin = usuarioLogin,
            PasswordHash = passwordHash,
            Activo = activo,
            UsuarioCreacion = usuarioCreacion,
            FechaCreacion = fechaCreacion
        };
        return usuario;
    }
}
