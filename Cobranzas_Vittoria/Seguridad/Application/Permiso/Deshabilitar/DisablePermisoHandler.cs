using Cobranzas_Vittoria.Seguridad.Application.Common;
using PermisoDomain = Cobranzas_Vittoria.Seguridad.Domain.Model.Permiso;
using Cobranzas_Vittoria.Seguridad.Domain.Persistence;
using Cobranzas_Vittoria.Seguridad.Domain.Excepciones;

namespace Cobranzas_Vittoria.Seguridad.Application.Permiso.Deshabilitar;

/// <summary> Handler para deshabilitar un permiso en el sistema de seguridad. </summary>
public class DisablePermisoHandler
{
    private readonly IPermisoRepository _permisoRepository;
    private readonly IUsuarioActualService _usuarioActualService;
    private readonly ILogger<DisablePermisoHandler> _logger;

    public DisablePermisoHandler(
        IPermisoRepository permisoRepository,
        IUsuarioActualService usuarioActualService,
        ILogger<DisablePermisoHandler> logger)
    {
        _permisoRepository = permisoRepository;
        _usuarioActualService = usuarioActualService;
        _logger = logger;
    }

    public async Task HandleAsync(DisablePermisoCommand command)
    {
        PermisoValidator.ValidarDisable(command);

        _logger.LogInformation("Deshabilitando permiso con IdPermiso={IdPermiso}", command.IdPermiso);

        PermisoDomain permiso = await _permisoRepository.GetByIdAsync(command.IdPermiso)
            ?? throw new ValidacionNegocioSeguridadException(
                nameof(command.IdPermiso),
                "PERMISO_NO_ENCONTRADO",
                $"No se encontro el permiso con Id {command.IdPermiso}.");

        permiso.Desactivar(_usuarioActualService.ObtenerUsuarioActual());

        await _permisoRepository.DisableAsync(permiso);

        _logger.LogInformation("Permiso deshabilitado: IdPermiso={IdPermiso}", permiso.IdPermiso);
    }
}
