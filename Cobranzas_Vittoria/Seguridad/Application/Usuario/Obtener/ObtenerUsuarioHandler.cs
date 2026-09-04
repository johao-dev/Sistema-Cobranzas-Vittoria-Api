using Cobranzas_Vittoria.Interfaces;

namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.Obtener;

public class ObtenerUsuarioHandler
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ILogger<ObtenerUsuarioHandler> _logger;

    public ObtenerUsuarioHandler(
        IUsuarioRepository usuarioRepository,
        ILogger<ObtenerUsuarioHandler> logger)
    {
        _usuarioRepository = usuarioRepository;
        _logger = logger;
    }

    public async Task<ObtenerUsuarioResult> HandleAsync(ObtenerUsuarioQuery query)
    {
        _logger.LogInformation(
            "Consultando usuario por IdUsuario={IdUsuario}",
            query.IdUsuario);

        Domain.Model.Usuario? usuario = await _usuarioRepository.GetByIdAsync(query.IdUsuario)
            ?? throw new KeyNotFoundException($"Usuario con Id {query.IdUsuario} no encontrado.");

        _logger.LogInformation(
            "Usuario encontrado: IdUsuario={IdUsuario}, Login={UsuarioLogin}",
            usuario.IdUsuario,
            usuario.UsuarioLogin);

        return new ObtenerUsuarioResult(
            usuario.IdUsuario,
            usuario.Nombres,
            usuario.Apellidos,
            usuario.Correo.Value,
            usuario.UsuarioLogin,
            usuario.Activo,
            usuario.FechaCreacion,
            usuario.UsuarioCreacion);
    }
}
