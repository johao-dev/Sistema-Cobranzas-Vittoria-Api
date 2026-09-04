using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.Listar;

public class ListarUsuarioHandler
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ILogger<ListarUsuarioHandler> _logger;

    public ListarUsuarioHandler(
        IUsuarioRepository usuarioRepository,
        ILogger<ListarUsuarioHandler> logger)
    {
        _usuarioRepository = usuarioRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<ListarUsuarioResult>> HandleAsync(ListarUsuarioQuery query)
    {
        _logger.LogInformation("Listando usuarios con estado activo={Activo}", query.Activo ?? true);

        var usuarios = await _usuarioRepository.GetAllAsync(query.Activo);
        var resultado = usuarios.ToList();

        _logger.LogInformation("Se encontraron {Cantidad} usuarios", resultado.Count);
        _logger.LogDebug("Usuarios listados: {@Usuarios}", resultado);

        return resultado.Select(u => new ListarUsuarioResult(
            u.IdUsuario,
            u.Nombres,
            u.Apellidos,
            u.Correo.Value,
            u.UsuarioLogin,
            u.Activo,
            u.FechaCreacion,
            u.UsuarioCreacion));
    }
}
