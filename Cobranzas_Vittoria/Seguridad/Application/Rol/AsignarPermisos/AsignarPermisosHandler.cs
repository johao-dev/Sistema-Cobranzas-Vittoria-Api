using Cobranzas_Vittoria.Seguridad.Application.Common;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;

namespace Cobranzas_Vittoria.Seguridad.Application.Rol.AsignarPermisos;

public sealed class AsignarPermisosHandler
{
    private readonly IRolRepository _rolRepository;
    private readonly IUsuarioActualService _usuarioActualService;
    private readonly ILogger<AsignarPermisosHandler> _logger;

    public AsignarPermisosHandler(
        IRolRepository rolRepository,
        IUsuarioActualService usuarioActualService,
        ILogger<AsignarPermisosHandler> logger)
    {
        _rolRepository = rolRepository;
        _usuarioActualService = usuarioActualService;
        _logger = logger;
    }

    public async Task HandleAsync(AsignarPermisosCommand command)
    {
        _ = await _rolRepository.GetByIdAsync(command.IdRol)
            ?? throw new ValidacionNegocioSeguridadException(
                nameof(command.IdRol),
                "ROL_NO_ENCONTRADO",
                $"No se encontro el rol con Id {command.IdRol}.");

        var permisos = command.IdPermisos.Distinct().ToList();
        if (permisos.Count == 0)
        {
            throw new ValidacionNegocioSeguridadException(
                nameof(command.IdPermisos),
                "ROL_PERMISOS_REQUERIDOS",
                "Debe proporcionar al menos un permiso para asignar.");
        }

        await _rolRepository.AsignarPermisosAsync(
            command.IdRol,
            permisos,
            _usuarioActualService.ObtenerUsuarioActual());

        _logger.LogInformation("Permisos asignados al rol IdRol={IdRol}", command.IdRol);
    }
}
