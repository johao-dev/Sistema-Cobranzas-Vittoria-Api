using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Rol.QuitarPermiso;

public sealed class QuitarPermisoHandler
{
    private readonly IRolRepository _rolRepository;
    private readonly ILogger<QuitarPermisoHandler> _logger;

    public QuitarPermisoHandler(IRolRepository rolRepository, ILogger<QuitarPermisoHandler> logger)
    {
        _rolRepository = rolRepository;
        _logger = logger;
    }

    public async Task HandleAsync(QuitarPermisoCommand command)
    {
        _ = await _rolRepository.GetByIdAsync(command.IdRol)
            ?? throw new ValidacionNegocioSeguridadException(
                nameof(command.IdRol),
                "ROL_NO_ENCONTRADO",
                $"No se encontro el rol con Id {command.IdRol}.");

        await _rolRepository.QuitarPermisoAsync(command.IdRol, command.IdPermiso);
        _logger.LogInformation(
            "Permiso IdPermiso={IdPermiso} removido del rol IdRol={IdRol}",
            command.IdPermiso,
            command.IdRol);
    }
}
