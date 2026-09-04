using Cobranzas_Vittoria.Interfaces;
using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Application.Usuario.Crear;

public class CreateUsuarioHandler
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IUsuarioActualService _usuarioActualService;
    private readonly ILogger<CreateUsuarioHandler> _logger;

    public CreateUsuarioHandler(
        IUsuarioRepository usuarioRepository,
        IUsuarioActualService usuarioActualService,
        ILogger<CreateUsuarioHandler> logger)
    {
        _usuarioRepository = usuarioRepository;
        _usuarioActualService = usuarioActualService;
        _logger = logger;
    }

    public async Task<CreateUsuarioResult> HandleAsync(CreateUsuarioCommand command)
    {
        _logger.LogInformation("Iniciando creacion de usuario {UsuarioLogin}", command.UsuarioLogin);
        _logger.LogDebug("Datos recibidos para crear usuario: {@Command}", command);

        UsuarioValidator.ValidarCreate(command);

        if (await _usuarioRepository.GetByCorreoAsync(command.Correo.Trim()) is not null)
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(command.Correo),
                "USUARIO_CORREO_DUPLICADO",
                $"Ya existe un usuario con el correo {command.Correo}.");
        }

        Domain.Model.Usuario usuario = Domain.Model.Usuario.Crear(
            command.Nombres,
            command.Apellidos,
            command.Correo,
            command.UsuarioLogin,
            command.PasswordHash,
            _usuarioActualService.ObtenerUsuarioActual());

        Domain.Model.Usuario usuarioCreado = await _usuarioRepository.AddAsync(usuario);

        _logger.LogInformation(
            "Usuario creado exitosamente: IdUsuario={IdUsuario}, Login={UsuarioLogin}",
            usuarioCreado.IdUsuario,
            usuarioCreado.UsuarioLogin);

        return new CreateUsuarioResult(
            usuarioCreado.IdUsuario,
            usuarioCreado.Nombres,
            usuarioCreado.Apellidos,
            usuarioCreado.Correo.Value,
            usuarioCreado.UsuarioLogin,
            usuarioCreado.Activo,
            usuarioCreado.FechaCreacion,
            usuarioCreado.UsuarioCreacion);
    }
}
