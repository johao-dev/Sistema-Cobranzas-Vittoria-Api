using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Rol.Crear;

public class CreateRolHandler
{
    private readonly IRolRepository _rolRepository;
    private readonly IUsuarioActualService _usuarioActualService;
    private readonly ILogger<CreateRolHandler> _logger;

    public CreateRolHandler(
        IRolRepository rolRepository,
        IUsuarioActualService usuarioActualService,
        ILogger<CreateRolHandler> logger)
    {
        _rolRepository = rolRepository;
        _usuarioActualService = usuarioActualService;
        _logger = logger;
    }

    public async Task<CreateRolResult> HandleAsync(CreateRolCommand command)
    {
        _logger.LogInformation("Iniciando creacion de rol con nombre {Nombre}", command.Nombre);
        _logger.LogDebug("Datos recibidos para crear rol: {@Command}", command);

        RolValidator.ValidarCreate(command);

        if (await _rolRepository.GetByNombreAsync(command.Nombre.Trim()) is not null)
            throw new ValidacionNegocioSeguridadException(
                nameof(command.Nombre),
                "ROL_NOMBRE_DUPLICADO",
                $"Ya existe un rol con el nombre {command.Nombre}");

        Domain.Model.Rol rol = Domain.Model.Rol.Crear(
            command.Nombre,
            command.Descripcion);

        rol.EstablecerAuditoriaCreacion(_usuarioActualService.ObtenerUsuarioActual());

        Domain.Model.Rol rolCreado = await _rolRepository.AddAsync(rol);

        _logger.LogInformation(
            "Rol creado exitosamente: IdRol={IdRol}, Nombre={Nombre}",
            rolCreado.IdRol,
            rolCreado.Nombre);

        return new CreateRolResult(
            rolCreado.IdRol,
            rolCreado.Nombre,
            rolCreado.Descripcion,
            rolCreado.Activo,
            rolCreado.FechaCreacion,
            rolCreado.UsuarioCreacion);
    }
}
